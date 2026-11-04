namespace Grsms.Web.Models;

/// <summary>Numbers shown on the Phase 1 home page.</summary>
public class HomeViewModel
{
    public int ActiveDepartments { get; set; }
    public int Units { get; set; }
    public int Sites { get; set; }
    public int StaffAccounts { get; set; }
    public int CandidateAccounts { get; set; }
}
