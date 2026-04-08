using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plugins;

namespace FreeGLBA.Server.Controllers;

public partial class DataController
{
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/ExecutePlugin")]
    public ActionResult<PluginExecuteRequest> ExecutePlugin(PluginExecuteRequest request)
    {
        var output = da.ExecutePlugin(request, CurrentUser);
        return Ok(output);
    }
}
