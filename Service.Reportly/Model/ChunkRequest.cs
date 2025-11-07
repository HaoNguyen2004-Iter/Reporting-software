using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Service.Reportly.Executes.Uploads;

namespace Service.Reportly.Model
{
    public sealed class ChunkRequest
    {
        public string? UploadId { get; set; }               // stable id per file from client; if null, will be generated on first chunk
        public string OriginalFileName { get; set; } = null!;
        public int ChunkIndex { get; set; }                 // 0-based
        public int TotalChunks { get; set; }                // total chunks for this file
        public long TotalSizeBytes { get; set; }            // full file size
    }

    public sealed class UploadChunkResult
    {
        public bool Completed { get; set; }
        public string UploadId { get; set; } = null!;
        public UploadModel? File { get; set; }              // set when Completed = true
    }
}
