using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Sprint.Data;
using Sprint.Enums;
using Sprint.Models;
using Sprint.ViewModels;

namespace Sprint.Controllers
{
    public class GameController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public GameController(UserManager<User> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: Game
        [HttpGet]
        public async Task<IActionResult> Index(string price, string category, string search)
        {
            // filter price
            if (!Enum.TryParse(price, out FilterPrice ePrice) && !string.IsNullOrWhiteSpace(price))
            {
                price = null;
                return RedirectToAction(nameof(Index), new { price, category, search });
            }

            // user
            User user = await _userManager.GetUserAsync(User);

            DateTime now = DateTime.Now;

            // query games
            IQueryable<Game> gamesQuery = _context.Games.Include(g => g.GameType);
            gamesQuery = FilterBySearch(gamesQuery, search);
            gamesQuery = FilterByPrice(gamesQuery, ePrice);
            gamesQuery = FilterByCategory(gamesQuery, category);

            var games = await gamesQuery
                .Select(g => new GameItemViewModel
                {
                    Game = g,
                    
                    Image = _context.GameImages
                        .FirstOrDefault(i => i.GameId == g.GameId && i.ImageType == ImageType.Banner),
                    
                    IsWishlisted = user != null && _context.UserGameWishlists
                        .Any(w => w.GameId == g.GameId && w.UserId == user.Id),
                    
                    IsInCart = user != null && _context.CartGames
                        .Any(c => c.GameId == g.GameId && c.CartUserId == user.Id && c.ReceivingUserId == user.Id),
                    
                    Discount = _context.GameDiscounts
                        .Where(d => d.GameId == g.GameId && d.DiscountPrice < g.RegularPrice && d.DiscountStart <= now && d.DiscountFinish > now)
                        .OrderBy(d => d.DiscountPrice)
                        .FirstOrDefault(),

                    IsOwned = user != null && _context.OrderItems
                        .Any(o => o.GameId == g.GameId && o.OwnerUserId == user.Id && !o.PhysicallyOwned),
                })
                .ToListAsync();

            // return view model
            var viewModel = new GameIndexViewModel
            {
                FilterPrice = price,
                FilterCategory = category,
                FilterSearch = search,

                FilterGroups = new List<FilterGroupViewModel>
                {
                    PopulateFilterPrice(ePrice),
                    await PopulateFilterCategoryAsync(category),
                },
                Games = games,
            };
            return View(viewModel);
        }

        // POST: Game
        [HttpPost, Route("[controller]")]
        public IActionResult Filter(string price, string category, string search)
        {
            return RedirectToAction(nameof(Index), new { price, category, search });
        }

        // GET: Game/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            DateTime now = DateTime.Now;
            User user = await _userManager.GetUserAsync(User);

            Game game = _context.Games
                .Include(g => g.GameType)
                .Include(g => g.Reviews)
                .Include(g => g.GameImages)
                .FirstOrDefault(g => g.GameId == id);

            if (game == null)
            {
                return NotFound();
            }

            return View(new GameViewModel
            {
                Game = game,

                Image = await _context.GameImages
                    .FirstOrDefaultAsync(i => i.GameId == game.GameId && i.ImageType == ImageType.Banner),

                Images = await _context.GameImages
                    .Where(i => i.GameId == game.GameId && i.ImageType == ImageType.Regular)
                    .ToListAsync(),

                Discount = await _context.GameDiscounts
                    .Where(d => d.GameId == game.GameId && d.DiscountPrice < game.RegularPrice && d.DiscountStart <= now && d.DiscountFinish > now)
                    .OrderBy(d => d.DiscountPrice)
                    .FirstOrDefaultAsync(),

                IsWishlisted = user != null && await _context.UserGameWishlists
                    .AnyAsync(w => w.GameId == game.GameId && w.UserId == user.Id),

                IsInCart = user != null && await _context.CartGames
                    .AnyAsync(c => c.GameId == game.GameId && c.CartUserId == user.Id && c.ReceivingUserId == user.Id),

                AverageRating = game.Reviews.Any() ? (int)game.Reviews.Select(review => review.Rating).Average() : 0,

                IsOwned = user != null && await _context.OrderItems
                    .AnyAsync(o => o.GameId == game.GameId && o.OwnerUserId == user.Id && !o.PhysicallyOwned),
            });
        }

        // GET: Game/Create
        public IActionResult Create()
        {
            ViewData["GameTypeId"] = new SelectList(_context.GameTypes, "GameTypeId", "Name");
            return View();
        }

        // POST: Game/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("GameId,GameTypeId,Name,Developer,Rating,RegularPrice")] Game game)
        {
            if (ModelState.IsValid)
            {
                _context.Add(game);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["GameTypeId"] = new SelectList(_context.GameTypes, "GameTypeId", "Name", game.GameTypeId);
            return View(game);
        }

        // GET: Game/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var game = await _context.Games.FindAsync(id);

            if (game == null)
            {
                return NotFound();
            }

            ViewData["GameTypeId"] = new SelectList(_context.GameTypes, "GameTypeId", "Name", game.GameTypeId);
            return View(game);
        }

        // POST: Game/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("GameId,GameTypeId,Name,Developer,Rating")] Game game)
        {
            if (id != game.GameId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(game);
                    await _context.SaveChangesAsync();  
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GameExists(game.GameId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            ViewData["GameTypeId"] = new SelectList(_context.GameTypes, "GameTypeId", "Game", game.GameTypeId);
            return View(game);
        }

        // GET: Game/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var game = await _context.Games
                .Include(g => g.GameType)
                .Include(g => g.GameImages)
                .FirstOrDefaultAsync(m => m.GameId == id);

            if (game == null)
            {
                return NotFound();
            }

            return View(game);
        }

        // POST: Game/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var game = await _context.Games.FindAsync(id);
            _context.Games.Remove(game);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private IQueryable<Game> FilterByPrice(IQueryable<Game> gamesQuery, FilterPrice price) => price switch
        {
            FilterPrice.u6 => gamesQuery.Where(g => g.RegularPrice < 6),
            FilterPrice.u12 => gamesQuery.Where(g => g.RegularPrice < 12),
            FilterPrice.u18 => gamesQuery.Where(g => g.RegularPrice < 18),
            FilterPrice.u24 => gamesQuery.Where(g => g.RegularPrice < 24),
            FilterPrice.u30 => gamesQuery.Where(g => g.RegularPrice < 30),
            FilterPrice.a30 => gamesQuery.Where(g => g.RegularPrice >= 30),
            FilterPrice.free => gamesQuery.Where(g => g.RegularPrice == 0),
            FilterPrice.discounted => gamesQuery.Where(g => g.Discounts.Any(d => d.DiscountStart <= DateTime.Now && d.DiscountFinish > DateTime.Now)),
            _ => gamesQuery,
        };

        private FilterGroupViewModel PopulateFilterPrice(FilterPrice price)
        {
            return new FilterGroupViewModel
            {
                Name = "Price",
                FormName = "price",
                Filters = new List<FilterItemViewModel>
                {
                    new FilterItemViewModel{ Name = "Under $6", Value = "u6", IsSelected = price == FilterPrice.u6 },
                    new FilterItemViewModel{ Name = "Under $12", Value = "u12", IsSelected = price == FilterPrice.u12 },
                    new FilterItemViewModel{ Name = "Under $18", Value = "u18", IsSelected = price == FilterPrice.u18 },
                    new FilterItemViewModel{ Name = "Under $24", Value = "u24", IsSelected = price == FilterPrice.u24 },
                    new FilterItemViewModel{ Name = "Under $30", Value = "u30", IsSelected = price == FilterPrice.u30 },
                    new FilterItemViewModel{ Name = "$30+", Value = "a30", IsSelected = price == FilterPrice.a30 },
                    new FilterItemViewModel{ Name = "Free", Value = "free", IsSelected = price == FilterPrice.free },
                    new FilterItemViewModel{ Name = "Discounted", Value = "discounted", IsSelected = price == FilterPrice.discounted },
                }
            };
        }

        private IQueryable<Game> FilterBySearch(IQueryable<Game> gamesQuery, string search)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                string pattern = $"%{search}%";
                return gamesQuery.Where(g => EF.Functions.Like(g.Name, pattern));
            }
            return gamesQuery;
        }

        private IQueryable<Game> FilterByCategory(IQueryable<Game> gamesQuery, string category)
        {
            if (!string.IsNullOrWhiteSpace(category))
            {
                return gamesQuery.Where(g => g.GameType.Name == category);
            }
            return gamesQuery;
        }

        private async Task<FilterGroupViewModel> PopulateFilterCategoryAsync(string category)
        {
            return new FilterGroupViewModel
            {
                Name = "Category",
                FormName = "category",
                Filters = await _context.GameTypes
                    .Include(t => t.Games)
                    .OrderBy(t => t.Name)
                    .Where(t => t.Games.Count > 0)
                    .Select(t => new FilterItemViewModel
                    {
                        Name = t.Name,
                        Count = t.Games.Count,
                        Value = t.Name.ToLower(),
                        IsSelected = t.Name == category,
                    })
                    .ToListAsync(),
            };
        }

        private bool GameExists(int id)
        {
            return _context.Games.Any(e => e.GameId == id);
        }
    }
}
