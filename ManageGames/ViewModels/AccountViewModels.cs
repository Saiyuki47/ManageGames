using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using ManageGames.Auth;
using ManageGames.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ManageGames.ViewModels;

/// <summary>Posted by the login form in the layout.</summary>
public class LoginViewModel
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ReturnUrl { get; set; }
}

public class ChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(PasswordPolicy.MaxLength, MinimumLength = PasswordPolicy.MinLength)]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords don't match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Repeat new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class UserListViewModel
{
    public IReadOnlyList<UserListItem> Users { get; set; } = [];
    public Guid CurrentUserId { get; set; }
}

public class CreateUserViewModel
{
    [Required]
    [StringLength(FieldLimits.UsernameMaxLength)]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(PasswordPolicy.MaxLength, MinimumLength = PasswordPolicy.MinLength)]
    [DataType(DataType.Password)]
    [Display(Name = "Initial password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Administrator")]
    [SuppressMessage("Major Bug", "S6964", Justification = "A checkbox: not posted means not checked.")]
    public bool IsAdmin { get; set; }
}

public class ResetPasswordViewModel
{
    [BindNever]
    [ValidateNever]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(PasswordPolicy.MaxLength, MinimumLength = PasswordPolicy.MinLength)]
    [DataType(DataType.Password)]
    [Display(Name = "Temporary password")]
    public string NewPassword { get; set; } = string.Empty;
}
