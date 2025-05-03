using Kaltura.Enums;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VimeoToKalturaVideoTransfer.Helper;

namespace VimeoToKalturaVideoTransfer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VideoTransferController : Controller
    {
        [HttpGet]
        public string TransferVideoFromVimeoToKaltura(string accessToken, string vimeoUserId, string schoolName)
        {
            VideoTransferHelper videoTransferHelper = new VideoTransferHelper(accessToken, vimeoUserId, schoolName);
            return videoTransferHelper.TransferVideoFromVimeoToKaltura();
        }
    }
}
