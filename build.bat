rmdir /s /q rel
mkdir rel

dotnet restore

msbuild -target:GarbageMan /p:Configuration=Release /p:Platform=x64 GarbageMan.sln
msbuild -target:GM /p:Configuration=Release /p:Platform=x64 GarbageMan.sln
msbuild -target:GM /p:Configuration=Release /p:Platform=x86 GarbageMan.sln

xcopy GarbageMan\bin\x64\Release\net5.0-windows\* rel\ /E /H
rmdir /s /q rel\runtimes\
mkdir rel\runtimes\win
xcopy GarbageMan\bin\x64\Release\net5.0-windows\runtimes\win rel\runtimes\win\ /E /H
mkdir rel\runtimes\win-x64
xcopy GarbageMan\bin\x64\Release\net5.0-windows\runtimes\win-x64 rel\runtimes\win-x64\ /E /H

mkdir rel\bin

mkdir rel\bin\x64
xcopy GM\bin\x64\Release\net5.0\* rel\bin\x64\ /E /H
rmdir /s /q rel\bin\x64\runtimes\
mkdir rel\bin\x64\runtimes\win-x64
xcopy GM\bin\x64\Release\net5.0\runtimes\win-x64 rel\bin\x64\runtimes\win-x64\ /E /H

mkdir rel\bin\x86
xcopy GM\bin\x86\Release\net5.0\* rel\bin\x86\ /E /H
rmdir /s /q rel\bin\x86\runtimes\
mkdir rel\bin\x86\runtimes\win-x86
xcopy GM\bin\x86\Release\net5.0\runtimes\win-x86 rel\bin\x86\runtimes\win-x86\ /E /H

copy Search.json rel\


powershell -File compress.ps1 -ExecutionPolicy Bypass