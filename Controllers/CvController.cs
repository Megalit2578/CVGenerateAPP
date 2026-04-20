using Microsoft.AspNetCore.Mvc;
using CVWebsite.Models;
using CVWebsite.Services;

namespace CVWebsite.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CvController : ControllerBase
{
    private readonly PdfService _pdfService;

    public CvController(PdfService pdfService) => _pdfService = pdfService;

    /// <summary>
    /// POST /api/cv/generate
    /// Accepts a CvModel as JSON, returns a PDF download.
    /// </summary>
    [HttpPost("generate")]
    public IActionResult Generate([FromBody] CvModel cv)
    {
        if (string.IsNullOrWhiteSpace(cv.FullName))
            return BadRequest(new { error = "Full name is required." });

        // Sanitise profile-picture size (max ~2 MB raw base64)
        if (cv.ProfilePicture?.Length > 2_800_000)
            cv.ProfilePicture = null; // discard oversized image rather than crash

        byte[] pdfBytes = _pdfService.GenerateCvPdf(cv);

        // Build a safe file name from the user's name
        var safeName = string.Concat(
            cv.FullName.Where(c => char.IsLetterOrDigit(c) || c == ' '))
            .Replace(" ", "_");

        return File(pdfBytes, "application/pdf", $"CV_{safeName}.pdf");
    }
}
