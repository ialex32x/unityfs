// //#define DOWNLOADER_LOW_SPEED
// using System;
// using System.Collections.Generic;
// using System.Net;
// using System.Security.Cryptography;
// using System.Threading;

// namespace ME.Assets.Dynamic
// {
//     using UnityEngine;

//     /// 后台下载器
//     public class Downloader
//     {
//         /// 单帧数据缓存大小
// #if DOWNLOADER_LOW_SPEED
//         public const int BufferSize = 1024 * 5;
// #else
//         public const int BufferSize = 1024 * 512;
// #endif

//         private string _remoteUrl;

//         private byte[] _buffer = new byte[BufferSize];
//         private MD5 _md5;
//         private System.Text.StringBuilder _tempStr = new System.Text.StringBuilder();

//         /// 下载请求队列
//         private LinkedList<PackageInfo> _requested = new LinkedList<PackageInfo>();

//         private AutoResetEvent _event;
//         private System.Threading.Thread _worker;

//         /// 创建一个下载器 (传入一个远程下载地址, 以及文件保存路径)
//         public Downloader(string remote)
//         {
//             _remoteUrl = remote;
//             _md5 = MD5.Create();
//             _event = new AutoResetEvent(false);

//             try
//             {
//                 if (!System.IO.Directory.Exists(PackageConfig.PackageCachePath))
//                     System.IO.Directory.CreateDirectory(PackageConfig.PackageCachePath);
//             }
//             catch (Exception exception)
//             {
//                 Debug.LogError(exception);
//             }
//         }

//         /// 请求下载指定路径的文件
//         public void OpenRequest(PackageInfo info)
//         {
//             if (info == null || info.state == EPackageState.Latest || info.state == EPackageState.Invalid)
//             {
//                 // 已经确认下载成功, 或无效 (不可挽回)
//                 //Debug.LogWarningFormat("latest or invalid {0}", info);
//                 return;
//             }

//             if (!info.isDone)
//             {
//                 // 已经打开
//                 //Debug.LogWarningFormat("already open {0}", info);
//                 return;
//             }

//             lock (_requested)
//             {
//                 if (!_requested.Contains(info))
//                 {
//                     info.Open();
//                     if (info.state == EPackageState.Latest)
//                     {
//                         info.Close();
//                         //Debug.LogWarningFormat("latest {0}", info);
//                         return;
//                     }
//                     _requested.AddLast(info);
//                 }
//             }
//             _Start();
//             return;
//         }

//         private void _Start()
//         {
//             if (_worker == null)
//             {
//                 _worker = new System.Threading.Thread(new ThreadStart(_DaemonThread));
//                 _worker.Priority = System.Threading.ThreadPriority.BelowNormal;
//                 _worker.Start();
//             }
//             _event.Set();
//         }

//         public void Stop()
//         {
//             if (_worker != null)
//             {
//                 _worker.Abort();
//                 _worker = null;
//             }
//         }

//         public void Close()
//         {
//             if (_worker != null)
//             {
//                 _worker.Abort();
//                 _worker = null;
//             }
//             if (_event != null)
//             {
//                 _event.Close();
//                 _event = null;
//             }
//         }

//         private string BytesToHexString(byte[] bytes)
//         {
//             if (_tempStr.Length > 0)
//                 _tempStr.Remove(0, _tempStr.Length);

//             for (var index = 0; index < bytes.Length; ++index)
//             {
//                 _tempStr.AppendFormat("{0:x}", bytes[index]);
//             }

//             return _tempStr.ToString();
//         }

//         /// 下载器后台线程
//         public void _DaemonThread()
//         {
//             while (true)
//             {
//                 //Debug.Log("wait one...");
//                 _event.WaitOne();

//                 if (_requested.Count > 0)
//                 {
//                     PackageInfo packageInfo = null;

//                     lock (_requested)
//                     {
//                         packageInfo = _requested.First.Value;
//                         _requested.RemoveFirst();
//                     }

//                     //Debug.LogFormat("开始下载 {0}", packageInfo.path);
//                     Download(packageInfo);
//                     //Debug.LogFormat("结束下载 {0}", packageInfo.path);
//                     packageInfo.Close();
//                 }
//             }
//         }

//         private bool Download(PackageInfo packageInfo)
//         {
// #if UNITY_WEBPLAYER
//             Debug.LogError(string.Format("{0} {1} {2}", _remoteUrl, _buffer, _md5));
//             Debug.LogError("WebPlayer不支持Downloader");
//             return false;
// #else
//             try
//             {
//                 if (packageInfo.state == EPackageState.Latest)
//                     return true;

//                 var pathWithSuffix = packageInfo.path + PackageConfig.PackageExtension;
//                 var remoteUrl = new Uri(_remoteUrl + pathWithSuffix);
//                 var localUrl = PackageConfig.PackageCachePath + pathWithSuffix;
//                 var req = (HttpWebRequest)WebRequest.Create(remoteUrl);
//                 var partialSize = 0L;
//                 req.Method = WebRequestMethods.Http.Get;
//                 req.ContentType = PackageConfig.ContentType;

//                 if (packageInfo.state == EPackageState.Partial)
//                 {
//                     partialSize = packageInfo.GetLocalSize();

//                     if (partialSize != 0)
//                     {
//                         req.AddRange((int)partialSize);
//                     }

//                     //Debug.LogFormat("续传位置 {0} @ {1}", packageInfo.path, partialSize);
//                 }
//                 else
//                 {
//                     packageInfo.Create();
//                 }

//                 using (var rsp = req.GetResponse())
//                 {
//                     if (rsp.ContentLength > 0)
//                     {
//                         using (var netStream = rsp.GetResponseStream())
//                         {
//                             var md5 = string.Empty;
//                             var sp1 = localUrl.LastIndexOf('/');
//                             var sp2 = localUrl.LastIndexOf('\\');

//                             var sp = Math.Max(sp1, sp2);

//                             if (sp > 0)
//                             {
//                                 var dir = localUrl.Substring(0, sp);

//                                 if (!System.IO.Directory.Exists(dir))
//                                     System.IO.Directory.CreateDirectory(dir);
//                             }

//                             using (var wfs = System.IO.File.Open(localUrl, System.IO.FileMode.OpenOrCreate))
//                             {
//                                 var total = 0L;
//                                 var seekSize = wfs.Seek(partialSize, System.IO.SeekOrigin.Begin);

//                                 if (seekSize != partialSize)
//                                 {
//                                     Debug.LogError("没有定位到预期位置!!!");
//                                 }

//                                 wfs.SetLength(partialSize);

//                                 while (total < rsp.ContentLength)
//                                 {
//                                     var step = netStream.Read(_buffer, 0, _buffer.Length);

//                                     if (step > 0)
//                                     {
//                                         total += step;
//                                         wfs.Write(_buffer, 0, step);
//                                         packageInfo.AppendSize(step);
//                                     }
//                                     else
//                                     {
//                                         //Debug.LogWarningFormat("NO DATA: {0}, {1}", packageInfo.path, step);
//                                     }
// #if DOWNLOADER_LOW_SPEED
//                                     System.Threading.Thread.Sleep(5);
// #endif
//                                     //Debug.LogFormat("ADD DATA: {0}, {1} @ Thread: {2}",
//                                     //    packageInfo.path, packageInfo.localSize, System.Threading.Thread.CurrentThread.ManagedThreadId);
//                                 }

//                                 wfs.Flush();
//                                 wfs.Seek(0L, System.IO.SeekOrigin.Begin);
//                                 md5 = BytesToHexString(_md5.ComputeHash(wfs));

//                                 wfs.Close();
//                                 wfs.Dispose();

//                                 //Debug.LogFormat("{0} md5.wfs: {1} md5.file: {2} # {3}",
//                                 //    packageInfo.path,
//                                 //    md5,
//                                 //    BytesToHexString(_md5.ComputeHash(System.IO.File.ReadAllBytes(localUrl))),
//                                 //    partialSize
//                                 //    );
//                             }
//                             packageInfo.Update(md5);
//                             netStream.Close();
//                             netStream.Dispose();
//                         }
//                     }
//                     else
//                     {
//                         Debug.LogErrorFormat("RESPONSE.ERR: {0} {1} {2}", packageInfo.path, rsp.ContentType, rsp.ContentLength);
//                     }

//                     rsp.Close();
//                 }
//                 return true;
//             }
//             catch (Exception e)
//             {
//                 Debug.LogError(e);
//             }

//             Debug.LogErrorFormat("[DLC] 下载失败 {0}", packageInfo.path);
//             packageInfo.Update(string.Empty);
//             return false;
// #endif
//         }
//     }
// }
