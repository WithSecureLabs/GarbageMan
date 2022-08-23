
$V=Select-Xml -Path .\GarbageMan\GarbageMan.csproj -XPath '/Project/PropertyGroup/Version' | ForEach-Object { $_.Node.InnerXML }
$G="GarbageMan-"+$V
$Z=$G+".zip"
Rename-Item rel $G
Compress-Archive -Path $G -DestinationPath $Z

