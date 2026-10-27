namespace SwitchYard.Service.Models
{
    public class PaginationQuery
    {
        private const int DefaultPageNumber = 1;
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 100;

        private int _pageNumber = DefaultPageNumber;
        private int _pageSize = DefaultPageSize;

        public int PageNumber
        {
            get => _pageNumber;
            set => _pageNumber = value < 1 ? DefaultPageNumber : value;
        }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (value < 1)
                {
                    _pageSize = DefaultPageSize;
                    return;
                }

                _pageSize = value > MaxPageSize ? MaxPageSize : value;
            }
        }

        public int Offset => (PageNumber - 1) * PageSize;
    }

    public sealed class UserPaginationQuery : PaginationQuery
    {
        public string? Keyword { get; set; }
    }
}
