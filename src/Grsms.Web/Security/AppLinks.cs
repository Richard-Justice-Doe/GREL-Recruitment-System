using Grsms.Application.Common.Interfaces;

namespace Grsms.Web.Security;

/// <summary>
/// Builds full addresses for links in notification emails.
/// Uses the address of the current web request (e.g. https://grsms.grel.example). If there is no request
/// (background jobs, later phases), it falls back to the "App:BaseUrl" setting.
/// </summary>
public class AppLinks(IHttpContextAccessor accessor, LinkGenerator linkGenerator, IConfiguration config) : IAppLinks
{
    public string RecruitmentRequest(int id) => Build("Details", "RecruitmentRequests", new { id });

    public string Vacancy(int id) => Build("Details", "Vacancies", new { id });

    public string Advert(int id) => Build("Details", "Adverts", new { id });

    public string Application(int id) => Build("Details", "Applications", new { id });

    public string MyApplication(int id) => Build("Details", "MyApplications", new { id });

    public string Shortlist(int vacancyId) => Build("Vacancy", "Shortlisting", new { id = vacancyId });

    public string Interview(int id) => Build("Details", "Interviews", new { id });

    public string HiringCase(int applicationId) => Build("Case", "Hiring", new { id = applicationId });

    public string Onboarding(int id) => Build("Details", "Onboarding", new { id });

    public string CareersJob(string advertNumber) => Build("Details", "Careers", new { id = advertNumber });

    private string Build(string action, string controller, object values)
    {
        if (accessor.HttpContext is { } http &&
            linkGenerator.GetUriByAction(http, action, controller, values) is { } url)
            return url;

        var baseUrl = (config["App:BaseUrl"] ?? "").TrimEnd('/');
        return baseUrl + linkGenerator.GetPathByAction(action, controller, values);
    }
}
