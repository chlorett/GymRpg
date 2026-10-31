namespace MyApp.Application.Dtos;

public static class ErrorCodes
{
    public const string ValidationFailed = "ValidationFailed";
    public const string EmailTaken = "EmailTaken";
    public const string UsernameTaken = "UsernameTaken";
    public const string UserNotFound = "UserNotFound";
    public const string NotATrainer = "NotATrainer";
    public const string ExerciseNotFound = "ExerciseNotFound";
    public const string TemplateNameTaken = "TemplateNameTaken";
    public const string TemplateNotFound = "TemplateNotFound";
    public const string TemplateAccessDenied = "TemplateAccessDenied";
    public const string InvalidCredentials = "InvalidCredentials";
}
