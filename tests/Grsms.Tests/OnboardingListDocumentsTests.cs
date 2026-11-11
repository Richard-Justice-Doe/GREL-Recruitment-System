using Grsms.Application.Onboarding;
using Grsms.Domain.Onboarding;
using Grsms.Tests.Infrastructure;

namespace Grsms.Tests;

/// <summary>docs/33 (11 November 2026): the onboarding list shows each new employee's documents at a glance.</summary>
public class OnboardingListDocumentsTests : OnboardingTestBase
{
    private async Task<OnboardingListItemDto> RowAsync(int id)
    {
        AsRecruiter();
        return (await Run(s => s.SearchAsync(new OnboardingFilter { Tab = "progress" }))).Single(o => o.Id == id);
    }

    [Fact]
    public async Task The_list_counts_missing_waiting_and_verified_documents()
    {
        var (id, _) = await SubmittedAsync(Today);
        var submitted = await RowAsync(id);
        Assert.True(submitted.DocsRequired > 0);
        Assert.Equal((0, 0, 0), (submitted.DocsVerified, submitted.DocsMissing, submitted.DocsRejected)); // everything uploaded
        Assert.True(submitted.DocsToVerify >= submitted.DocsRequired);

        await VerifyAllAsync(id);
        var verified = await RowAsync(id);
        Assert.Equal((verified.DocsRequired, 0, 0), (verified.DocsVerified, verified.DocsMissing, verified.DocsToVerify));
    }

    [Fact]
    public async Task A_new_onboarding_shows_its_required_documents_as_missing()
    {
        var app = await WithOfferAsync(_candidate, "Ama Mensah", Grsms.Domain.Hiring.OfferStatus.Accepted, Today);
        var id = await StartAsync(app, Today);
        var row = await RowAsync(id);
        Assert.True(row.DocsRequired > 0);
        Assert.Equal((row.DocsRequired, 0, 0), (row.DocsMissing, row.DocsVerified, row.DocsToVerify));
        Assert.Equal(row.DocsRequired, (await DocumentsAsync(id)).Count(d => d.IsRequired && d.IsApplicable && d.Status == EmployeeDocumentStatus.Pending));
    }
}
