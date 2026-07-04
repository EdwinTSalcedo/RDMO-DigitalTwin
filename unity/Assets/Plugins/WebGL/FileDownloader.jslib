mergeInto(LibraryManager.library, {
    DownloadFileFromBase64: function(fileNamePtr, base64Ptr) {
        var fileName = UTF8ToString(fileNamePtr);
        var base64 = UTF8ToString(base64Ptr);
        
        var link = document.createElement('a');
        link.download = fileName;
        link.href = 'data:image/png;base64,' + base64;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
});
