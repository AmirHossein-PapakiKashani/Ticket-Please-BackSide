using Ticket.Domain.Enums;

namespace Ticket.Application.DTOs;

public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(string AccessToken, string RefreshToken, int UserId, string RoleName, string FullName);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record TokenResponse(string AccessToken, string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Username);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record UserProfile(string FullName, string Username, string PhoneNumber, string RoleName, string? OrgName);
public sealed record UpdateProfileRequest(string FullName, string PhoneNumber);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record SubscriptionInfo(string PlanName, DateTime ExpireDate, int RemainingDays);
public sealed record ProfileSummary(string FullName, string RoleName, string? AvatarUrl);
public sealed record UnreadCountResponse(int UnreadCount);
public sealed record NotificationItem(int Id, string Type, string Title, string? Body, int? RefId, bool IsRead, DateTime CreatedAt);

public static class NotificationTypeNames
{
    public static string ToApi(NotificationType type) => type.ToString();
}
