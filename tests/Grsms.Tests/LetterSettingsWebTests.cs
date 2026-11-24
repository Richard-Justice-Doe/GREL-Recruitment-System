using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Grsms.Domain.Settings;
using Grsms.Tests.Infrastructure;

namespace Grsms.Tests;

/// <summary>
/// 12 November 2026: Administration → Letterhead &amp; signature. The Super Admin sets the address, contacts and signatory
/// and uploads a signature (images only); the letter preview shows them; the signature is never served to anonymous visitors;
/// these fields are not on the System settings screen.
/// </summary>
public class LetterSettingsWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes, string fileName, string contentType)
    {
        var page = await client.GetStringAsync("/LetterSettings");
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { new StringContent(WebUtility.HtmlDecode(token)), "__RequestVerificationToken" } };
        form.Add(file, "signature", fileName);
        return await client.PostAsync("/LetterSettings/UploadSignature", form);
    }

    [Fact]
    public async Task Super_admin_sets_the_letterhead_and_signature_and_the_letters_show_them()
    {
        var admin = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await admin.SignInAsync(GrsmsWebFactory.AdminEmail, GrsmsWebFactory.AdminPassword)).StatusCode);
        Assert.Contains("/LetterSettings", await admin.GetStringAsync("/Administration"));
        Assert.DoesNotContain("Signatory", await admin.GetStringAsync("/SystemSettings"));

        // Defaults: company name, "Human Resources", space to sign by hand.
        var page = await admin.GetStringAsync("/LetterSettings");
        Assert.Contains("Ghana Rubber Estates Limited", page);
        Assert.Contains("letter-signature-space", page);

        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/LetterSettings", "/LetterSettings/Save", new()
        {
            [SettingCatalogue.LetterCompany] = "Ghana Rubber Estates Limited",
            [SettingCatalogue.LetterAddress] = "P.O. Box 228\nTakoradi, Ghana",
            [SettingCatalogue.LetterPhone] = "+233 31 000 0000",
            [SettingCatalogue.LetterEmail] = "hr@grel.test",
            [SettingCatalogue.LetterSignatoryName] = "Ama Mensah",
            [SettingCatalogue.LetterSignatoryTitle] = "Human Resources Manager"
        })).StatusCode);
        page = await admin.GetStringAsync("/LetterSettings");
        Assert.Contains("P.O. Box 228<br />", page);
        Assert.Contains("Tel &#x2B;233 31 000 0000", page); // Razor encodes "+"
        Assert.Contains("<strong>Ama Mensah</strong>", page);
        Assert.Contains("Human Resources Manager", page);

        // Only PNG or JPEG (checked from the content, not the name).
        await UploadAsync(admin, "%PDF-1.4"u8.ToArray(), "sig.png", "image/png");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/LetterImages/Signature")).StatusCode);

        Assert.Equal(HttpStatusCode.Redirect, (await UploadAsync(admin, Png, "sig.png", "image/png")).StatusCode);
        var image = await admin.GetAsync("/LetterImages/Signature");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
        Assert.Contains("/LetterImages/Signature", await admin.GetStringAsync("/LetterSettings"));

        // Never public.
        var anonymous = factory.NewClient();
        Assert.Contains("/Account/Login", (await anonymous.GetAsync("/LetterImages/Signature")).Headers.Location!.ToString());

        await admin.PostFormAsync("/LetterSettings", "/LetterSettings/RemoveSignature", new());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/LetterImages/Signature")).StatusCode);
    }
}
