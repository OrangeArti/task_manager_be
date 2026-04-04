using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Dtos.Comments;

public sealed record CreateCommentRequest(
    [Required][MaxLength(4000)] string Content
);
