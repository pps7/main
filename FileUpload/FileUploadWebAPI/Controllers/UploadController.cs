using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.IO;
using System.Web;
using System.Threading.Tasks;
using System.Net.Http.Headers;

namespace FileUploadWebAPI.Controllers
{
    public class UploadController : ApiController
    {
        [HttpPost]
        [Route("api/upload")]
        public HttpResponseMessage UploadFile()
        {
            foreach (string file in HttpContext.Current.Request.Files)
            {
                var FileDataContent = HttpContext.Current.Request.Files[file];
                if (FileDataContent != null && FileDataContent.ContentLength > 0)
                {
                    // take the input stream, and save it to a temp folder using  
                    // the original file.part name posted  
                    var stream = FileDataContent.InputStream;
                    var fileName = Path.GetFileName(FileDataContent.FileName);
                    var UploadPath = HttpContext.Current.Server.MapPath("~/App_Data/uploads");
                    Directory.CreateDirectory(UploadPath);
                    string path = Path.Combine(UploadPath, fileName);
                    try
                    {
                        if (System.IO.File.Exists(path))
                            System.IO.File.Delete(path);
                        using (var fileStream = System.IO.File.Create(path))
                        {
                            stream.CopyTo(fileStream);
                        }
                        // Once the file part is saved, see if we have enough to merge it  
                        MergeFile(path);
                    }
                    catch (IOException ex)
                    {
                        // handle  
                    }
                }
            }
            return new HttpResponseMessage()
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent("File uploaded.")
            };
        }

        public bool MergeFile(string FileName)
        {
            bool rslt = false;
            // parse out the different tokens from the filename according to the convention  
            string partToken = ".part_";
            string baseFileName = FileName.Substring(0, FileName.IndexOf(partToken));
            string trailingTokens = FileName.Substring(FileName.IndexOf(partToken) + partToken.Length);
            int FileIndex = 0;
            int FileCount = 0;
            int.TryParse(trailingTokens.Substring(0, trailingTokens.IndexOf(".")), out FileIndex);
            int.TryParse(trailingTokens.Substring(trailingTokens.IndexOf(".") + 1), out FileCount);
            // get a list of all file parts in the temp folder  
            string Searchpattern = Path.GetFileName(baseFileName) + partToken + "*";
            string[] FilesList = Directory.GetFiles(Path.GetDirectoryName(FileName), Searchpattern);
            //  merge .. improvement would be to confirm individual parts are there / correctly in  
            // sequence, a security check would also be important  
            // only proceed if we have received all the file chunks  
            if (FilesList.Count() == FileCount)
            {
                // use a singleton to stop overlapping processes  
                //if (!MergeFileManager.Instance.InUse(baseFileName))
                //{
                //    MergeFileManager.Instance.AddFile(baseFileName);
                if (File.Exists(baseFileName))
                    File.Delete(baseFileName);
                // add each file located to a list so we can get them into  
                // the correct order for rebuilding the file  
                List<SortedFile> MergeList = new List<SortedFile>();
                foreach (string File in FilesList)
                {
                    SortedFile sFile = new SortedFile();
                    sFile.FileName = File;
                    baseFileName = File.Substring(0, File.IndexOf(partToken));
                    trailingTokens = File.Substring(File.IndexOf(partToken) + partToken.Length);
                    int.TryParse(trailingTokens.
                       Substring(0, trailingTokens.IndexOf(".")), out FileIndex);
                    sFile.FileOrder = FileIndex;
                    MergeList.Add(sFile);
                }
                // sort by the file-part number to ensure we merge back in the correct order  
                var MergeOrder = MergeList.OrderBy(s => s.FileOrder).ToList();
                using (FileStream FS = new FileStream(baseFileName, FileMode.Create))
                {
                    // merge each file chunk back into one contiguous file stream  
                    foreach (var chunk in MergeOrder)
                    {
                        try
                        {
                            using (FileStream fileChunk =
                               new FileStream(chunk.FileName, FileMode.Open))
                            {
                                fileChunk.CopyTo(FS);
                            }

                            File.Delete(chunk.FileName);
                        }
                        catch (IOException ex)
                        {
                            // handle  
                        }
                    }
                }
                rslt = true;
                // unlock the file from singleton  
                //    MergeFileManager.Instance.RemoveFile(baseFileName);
                //}
            }
            return rslt;
        }

        [HttpGet]
        [Route("api/getVideo")]
        public HttpResponseMessage GetVideoContent()
        {
            var httpResponse = Request.CreateResponse();
            long range = 0;
            if (Request.Headers.Range.Ranges.Count > 0)
            {
                var rn = Request.Headers.Range.Ranges.ToList();
                if (rn[0].From.HasValue)
                    range = rn[0].From.Value;
            }
            httpResponse.Content = new PushStreamContent((Stream outputStream, HttpContent content, TransportContext transportContext) => { WriteContentToStream(outputStream, content, transportContext, range); });
            return httpResponse;
        }

        public async void WriteContentToStream(Stream outputStream, HttpContent content, TransportContext transportContext, long range)
        {
            //path of file which we have to read//  
            var filePath = HttpContext.Current.Server.MapPath("~/App_Data/uploads/DC Films Presents  Dawn of the Justice League 2_5.mp4");
            //here set the size of buffer, you can set any size  
            int bufferSize = 1000;
            byte[] buffer = new byte[bufferSize];
            //here we re using FileStream to read file from server//  
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fileStream.Seek(range, SeekOrigin.Begin);
                int totalSize = (int)(fileStream.Length - range);
                /*here we are saying read bytes from file as long as total size of file 
 
                is greater then 0*/
                while (totalSize > 0)
                {
                    int count = totalSize > bufferSize ? bufferSize : totalSize;
                    //here we are reading the buffer from orginal file  
                    int sizeOfReadedBuffer = fileStream.Read(buffer, 0, count);
                    //here we are writing the readed buffer to output//  
                    await outputStream.WriteAsync(buffer, 0, sizeOfReadedBuffer);
                    //and finally after writing to output stream decrementing it to total size of file.  
                    totalSize -= sizeOfReadedBuffer;
                }
            }
        }

        [HttpGet]
        [Route("api/getVideo2")]
        public HttpResponseMessage GetVideoContent2()
        {

            var filePath = HttpContext.Current.Server.MapPath("~/App_Data/uploads/DC Films Presents  Dawn of the Justice League 2_5.mp4");
            if (!File.Exists(filePath))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            var response = Request.CreateResponse();
            response.Headers.AcceptRanges.Add("bytes");

            var streamer = new FileStreamer();
            streamer.FileInfo = new FileInfo(filePath);
            response.Content = new PushStreamContent(streamer.WriteToStream);

            RangeHeaderValue rangeHeader = Request.Headers.Range;
            if (rangeHeader != null)
            {
                long totalLength = streamer.FileInfo.Length;
                var range = rangeHeader.Ranges.First();
                streamer.Start = range.From ?? 0;
                streamer.End = range.To ?? totalLength - 1;

                response.Content.Headers.ContentLength = streamer.End - streamer.Start + 1;
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(streamer.Start, streamer.End,
                    totalLength);
                response.StatusCode = HttpStatusCode.PartialContent;
            }
            else
            {
                response.StatusCode = HttpStatusCode.OK;
            }

            return response;
        }
    }

    public class SortedFile
    {
        public string FileName { get; set; }
        public int FileOrder { get; set; }
    }

    public class FileStreamer
    {
        public FileInfo FileInfo { get; set; }
        public long Start { get; set; }
        public long End { get; set; }

        public async Task WriteToStream(Stream outputStream, HttpContent content, TransportContext context)
        {
            try
            {
                var buffer = new byte[65536];
                using (var video = FileInfo.OpenRead())
                {
                    if (End == -1)
                    {
                        End = video.Length;
                    }
                    var position = Start;
                    var bytesLeft = End - Start + 1;
                    video.Position = Start;
                    while (position <= End)
                    {
                        var bytesRead = video.Read(buffer, 0, (int)Math.Min(bytesLeft, buffer.Length));
                        await outputStream.WriteAsync(buffer, 0, bytesRead);
                        position += bytesRead;
                        bytesLeft = End - position + 1;
                    }
                }
            }
            catch (Exception ex)
            {
                // fail silently
            }
            finally
            {
                outputStream.Close();
            }
        }
    }
}
