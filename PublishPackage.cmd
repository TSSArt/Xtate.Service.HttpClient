@pushd "%~dp0"
@for /D /R src %%i in (Release) do @for %%j in (%%~fi\*.nupkg) do dotnet nuget push "%%j" --source local --skip-duplicate
@popd