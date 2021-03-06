using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Sprint.Models
{
    public class CartGame
    {
        public int CartGameId { get; set; }
        public int CartUserId { get; set; }
        [DisplayName("Receiving User")]
        public int ReceivingUserId { get; set; }
        public int GameId { get; set; }
        [Required]
        [DisplayName("Added on")]
        public DateTime? AddedOn { get; set; }

        public virtual User CartUser { get; set; }
        public virtual User ReceivingUser { get; set; }
        public virtual Game Game { get; set; }
    }
}
