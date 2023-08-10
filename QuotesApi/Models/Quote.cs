using System.ComponentModel.DataAnnotations;

namespace QuotesApi.Models
{
    public class Quote
    {
        [Key]
        public int Id { get; set; }

        public string Author { get; set; }

        public string Text { get; set; }

        public int Length { get; set; }
    }

    public class Create
    {

        [Required]
        public string Author { get; set; }

        [Required]
        public string Text { get; set; }
    }
}
