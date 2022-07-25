using System.Threading.Tasks;

namespace Samples.HotChocolate
{
    public class BookAddedPayload
    {
        public Book Book { get; set; }

    }

    public class Mutation
    {
        public async Task<BookAddedPayload> AddBook(Book book)
        {
            return new BookAddedPayload { Book = book };
        }
    }
}
