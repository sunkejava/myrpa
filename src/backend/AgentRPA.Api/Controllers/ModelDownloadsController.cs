using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentRPA.Api.Controllers;

[ApiController, Route("api/model-downloads"), Authorize]
public sealed class ModelDownloadsController : ControllerBase
{
    [HttpGet("template")]
    public IActionResult Template()
    {
        var stream = typeof(ModelDownloadsController).Assembly.GetManifestResourceStream("ModelDownload.Template.xlsx")
            ?? throw new InvalidOperationException("缺少模型下载 Excel 模板。");
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "model-download-template.xlsx");
    }
}
