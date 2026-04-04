using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Dtos.Comments;

public sealed record CommentDto(
    int Id,
    int TaskId,
    string AuthorId,
    string AuthorName,
    string Content,
    DateTime CreatedAt
);
