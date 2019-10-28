
$(document).ready(function () {
    $('#btnUpload').click(function () {
        UploadFile($('#uploadFile')[0].files);
    }
    )
});

var PartCount = 0;

function UploadFile(TargetFile) {
    // create array to store the buffer chunks  
    var FileChunk = [];
    // the file object itself that we will work with  
    var file = TargetFile[0];
    // set up other initial vars  
    var MaxFileSizeMB = 1;
    var BufferChunkSize = MaxFileSizeMB * (1024 * 1024);
    var ReadBuffer_Size = 1024;
    var FileStreamPos = 0;
    // set the initial chunk length  
    var EndPos = BufferChunkSize;
    var Size = file.size;

    // add to the FileChunk array until we get to the end of the file  
    while (FileStreamPos < Size) {
        // "slice" the file from the starting position/offset, to  the required length  
        FileChunk.push(file.slice(FileStreamPos, EndPos));
        FileStreamPos = EndPos; // jump by the amount read  
        EndPos = FileStreamPos + BufferChunkSize; // set next chunk length  
    }
    // get total number of "files" we will be sending  
    var TotalParts = FileChunk.length;
    PartCount = 0;
    // loop through, pulling the first item from the array each time and sending it  
    var i = 0;
    var chunk;
    var promises = [];
    while (i++ < TotalParts - 1) {
        chunk = FileChunk.shift();
        PartCount++;
        // file name convention  
        var FilePartName = file.name + ".part_" + PartCount + "." + TotalParts;
        // send the file  
        promises.push(UploadFileChunk(chunk, FilePartName));
    }
    Promise.all(promises)
    .then(function () {
        chunk = FileChunk.shift();
        PartCount++;
        // file name convention  
        var FilePartName = file.name + ".part_" + PartCount + "." + TotalParts;
        // send the file  
        UploadFileChunk(chunk, FilePartName);
    });
}


function UploadFileChunk(Chunk, FileName) {
    var FD = new FormData();
    FD.append('file', Chunk, FileName);
    var dfr = $.Deferred();
    $.ajax({
        type: "POST",
        url: '/api/upload',
        contentType: false,
        processData: false,
        data: FD,
        success: function () {
            var width = $("#progressBar")[0].style.width;
            width = width.split("px")[0];
            width = parseInt(width);
            $("#progressBar")[0].style.width = width + (500 / PartCount) + "px";
            dfr.resolve();
        }
    });
    return dfr.promise();
}