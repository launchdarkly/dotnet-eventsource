
# see: https://stackoverflow.com/questions/27289115/system-io-compression-zipfile-net-4-5-output-zip-in-not-suitable-for-linux-mac
class FixZipFilePathEncoding : System.Text.UTF8Encoding {
    FixZipFilePathEncoding() : base($true) { }
    [byte[]] GetBytes([string] $s)
    {
        $s = $s.Replace("\", "/");
        return ([System.Text.UTF8Encoding]$this).GetBytes($s);
    }
}

function Zip {
    param(
        [Parameter(Mandatory)][string]$sourcePath,
        [Parameter(Mandatory)][string]$zipFile
    )
    [Reflection.Assembly]::LoadWithPartialName( "System.IO.Compression.FileSystem" ) | Out-Null
    [System.IO.Compression.ZipFile]::CreateFromDirectory($sourcePath, $zipFile, `
        [System.IO.Compression.CompressionLevel]::Optimal, $false,
        [FixZipFilePathEncoding]::new())
}
