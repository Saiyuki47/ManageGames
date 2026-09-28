using System.ComponentModel.DataAnnotations;
using ManageGames.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ManageGames.ViewModels
{
    public class ConsoleListViewModel
    {
        public List<ConsoleModel> Consoles { get; set; } = [];
        public string? Search { get; set; }
    }

    /// <summary>Add and edit form of a console; <see cref="Id"/> is null while adding.</summary>
    public class ConsoleFormViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(FieldLimits.NameMaxLength)]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Company")]
        public int? CompanyId { get; set; }

        [BindNever]
        [ValidateNever]
        public List<SelectListItem> CompanyOptions { get; set; } = [];
    }
}
