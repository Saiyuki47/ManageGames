namespace ManageGames.ViewModels;

/// <summary>Input limits shared by the forms, enforced server-side through model validation.</summary>
public static class FieldLimits
{
    public const int NameMaxLength = 100;
    public const int UsernameMaxLength = 50;
    public const int CopiesMax = 9999;
}
