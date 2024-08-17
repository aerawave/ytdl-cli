# YouTube Download (YTDL)

This is a quick and dirty solution to make a command-line interface (CLI) for downloading youtube videos.

# How to Use

Here is how to use this project, as well as set it up for working anywhere on your PC.

Also this has only been tested to be working on Windows 10 because that is all I needed it for.

1. Clone this repository with git (`git clone https://github.com/aerawave/ytdl-cli`), or alternatively just download the source code from it.
2. Build the project from the root directory of the repository with `dotnet build ytdl`
3. Ensure you have [ffmpeg](https://ffmpeg.org/) installed. It will need to be accessible anywhere (such as via your `PATH` variable) or you can specifically provide its location to the program with the `-ffmpeg` option.
4. Either modify your `PATH` environment variable to include the location of the `ytdl.exe`, or do what I do which is create a directory on the root of my C drive entitled `static_`, add that path (`C:/static_`) to my PATH variable, then add a powershell script to it, such as the [ytdl.ps1](/ytdl.ps1) found in this repository. Be sure to modify that file so that the path to the exe is correct.
5. You should be good to go!

## Available options:

-   `-o`: This will provide the destination directory for all files (both the downloaded ones and the final combined one). Usage: `-o <OUT_DIR>`
-   `-ffmpeg`: Use this to provide the location of your ffmpeg.exe if it's not already available in your `PATH` variable. Usage: `-ffmpeg <PATH_TO_FFMPEG_EXECUTABLE>`

## Example Calls

```
ytdl ytdl://youtube.com/watch?v=jNQXAC9IVRw ytdl://youtube.com/embed/jNQXAC9IVRw ytdl://youtu.be/jNQXAC9IVRw

ytdl ytdl://youtube.com/watch?v=jNQXAC9IVRw -o ./output

ytdl -ffmpeg C:/path/to/ffmpeg.exe ytdl://youtu.be/jNQXAC9IVRw
```

## Example output

```
Looking up video 'jNQXAC9IVRw'...
 -  Downloading file for 'jNQXAC9IVRw'... ( 100.00% ) 0.29 / 0.29 MB
 -  Downloading file for 'jNQXAC9IVRw'... ( 100.00% ) 0.75 / 0.75 MB
Video 'jNQXAC9IVRw' downloaded as audio and video! Now combining them with ffmpeg...
Video/audio combined for video 'jNQXAC9IVRw'... Cleaning up.
Video complete!: 'jNQXAC9IVRw - Me at the zoo'
```
