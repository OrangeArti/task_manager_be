using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace TaskManager.Api.Dtos
{
    public sealed class TaskListQuery
    {
        private const int MaxPageSize = 100;

        /// <summary>Номер страницы, начиная с 1. По умолчанию 1.</summary>
        [Range(1, int.MaxValue)]
        public int Page { get; init; } = 1;

        /// <summary>Размер страницы. По умолчанию 20. Максимум 100.</summary>
        [Range(1, MaxPageSize)]
        public int PageSize { get; init; } = 20;

        /// <summary>Поле сортировки: createdAt | dueDate | priority | title.</summary>
        public string? SortBy { get; init; } = "createdAt";

        /// <summary>Направление сортировки: asc | desc.</summary>
        public string? SortDir { get; init; } = "desc";

        // --- Фильтры ---
        /// <summary>Показывать завершённые/незавершённые. null — не фильтровать.</summary>
        public bool? IsCompleted { get; init; }

        /// <summary>Точный приоритет: 0=Low,1=Medium,2=High. null — не фильтровать.</summary>
        [Range(0, 2)]
        public int? Priority { get; init; }

        /// <summary>Поиск по Title/Description (contains, case-insensitive).</summary>
        public string? Search { get; init; }

        /// <summary>Дедлайн не раньше этой даты (UTC). null — без нижней границы.</summary>
        public DateTime? DueDateFrom { get; init; }

        /// <summary>Дедлайн не позже этой даты (UTC). null — без верхней границы.</summary>
        public DateTime? DueDateTo { get; init; }

        [StringLength(32)]
        public string? VisibilityScope { get; set; }

        // нормализуем значения, чтобы избежать if в контроллере
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

            // whitelist полей
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
