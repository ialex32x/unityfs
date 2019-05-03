using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    //NOTE: 因为 StreamingAssets 无法方便地进行同步加载文件内容/同步获取文件信息等
    //      所以考虑在生成初始包资源复制 StreamingAssets 时就计算生成所有相关信息, 存入 Resources 中
}
