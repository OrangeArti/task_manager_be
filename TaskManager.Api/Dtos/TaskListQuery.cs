using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace TaskManager.Api.Dtos
{
    public sealed class TaskListQuery
    {
        private const int MaxPageSize = 100;

        /// <summary>Page number starting from 1. Default is 1.</summary>
        [Range(1, int.MaxValue)]
        public int Page { get; init; } = 1;

        /// <summary>Page size. Default is 20. Maximum is 100.</summary>
        [Range(1, MaxPageSize)]
        public int PageSize { get; init; } = 20;

        /// <summary>Sort field: createdAt | dueDate | priority | title.</summary>
        public string? SortBy { get; init; } = "createdAt";

        /// <summary>Sort direction: asc | desc.</summary>
        public string? SortDir { get; init; } = "desc";

        // --- Filters ---
        /// <summary>Show completed/incompleted tasks; null means no filtering.</summary>
        public bool? IsCompleted { get; init; }

        /// <summary>Exact priority: 0=Low,1=Medium,2=High; null means no filtering.</summary>
        [Range(0, 2)]
        public int? Priority { get; init; }

        /// <summary>Search by Title/Description (contains, case-insensitive).</summary>
        public string? Search { get; init; }

        /// <summary>Due date not earlier than this UTC date; null means no lower bound.</summary>
        public DateTime? DueDateFrom { get; init; }

        /// <summary>Due date not later than this UTC date; null means no upper bound.</summary>
        public DateTime? DueDateTo { get; init; }

        [StringLength(32)]
        public string? VisibilityScope { get; set; }

        // normalize values to avoid extra branching in the controller
        public (int page, int pageSize) NormalizePaging()
        {
            var p = Page < 1 ? 1 : Page;
            var ps = PageSize < 1 ? 1 : (PageSize > MaxPageSize ? MaxPageSize : PageSize);
            return (p, ps);
        }

        public (string sortBy, bool desc) NormalizeSorting()
        {
            var field = (SortBy ?? "createdAt").Trim().ToLowerInvariant();
            var dir = (SortDir ?? "desc").Trim().ToLowerInvariant();
            var isDesc = dir == "desc";

            // whitelist allowed fields
            field = field switch
            {
                "createdat" => "createdAt",
                "duedate" => "dueDate",
                "priority" => "priority",
                "title" => "title",
                _ => "createdAt"
            };

            return (field, isDesc);
        }
        public string? NormalizeSearch()
        {
            var s = Search?.Trim();
            return string.IsNullOrEmpty(s) ? null : s;
        }

        public string? NormalizeVisibilityScope()
        {
            var value = VisibilityScope?.Trim();
            if (string.IsNullOrEmpty(value))
                return null;

            return value;
        }
    }
}
