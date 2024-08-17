using Newtonsoft.Json;
using System.Diagnostics;
using VideoLibrary;
using VideoLibrary.Debug;

namespace ytdl
{
    internal class Program
    {
        private const string EMBED = "https://www.youtube.com/embed";
        private static string FFMPEG_EXE = "ffmpeg.exe";

        static void Main(string[] args)
        {
            var argl = args.ToList();

            var options = new Dictionary<string, string>();
            var available_options = new string[]
            {
                "o",
                "ffmpeg"
            };



            // Search for the above options...
            var look_for_options = true;
            while (look_for_options)
            {
                look_for_options = false;

                for (var i = 0; i < argl.Count; i++)
                {
                    if (argl[i].StartsWith('-'))
                    {
                        var option = argl[i].TrimStart('-');
                        if (!available_options.Contains(option))
                            throw new Exception($"Bad option: {option}");

                        options.Add(option, argl[i + 1]);

                        argl.RemoveRange(i, 2);

                        look_for_options = true;
                        break;
                    }
                }
            }

            // Defaults output to downloads folder
            if (!options.TryGetValue("o", out string? output_location))
                output_location = "%USERPROFILE%/Downloads";

            // Update ffmpeg location if provided
            if (options.TryGetValue("ffmpeg", out string? ffmpeg_location))
                FFMPEG_EXE = ffmpeg_location;

            var video_ids = new List<string>();

            if (argl.Count < 1)
            {
                Console.WriteLine("No URIs provided.");
                return;
            }

            // exctract video IDs from provided URIs
            foreach (var url in argl)
            {
                try
                {
                    video_ids.Add(GetVideoId(url));
                } catch (Exception exception)
                {
                    Console.WriteLine($"There was an error extracting video ID from '{url}': {exception.Message}");
                }
            }

            var out_dir = Environment.ExpandEnvironmentVariables(output_location);
            var out_file = "{video.id} - {video.title}";
            var out_error = "issue-{video.id}.json";

            var tasks = new List<Task>();

            foreach (var video_id in video_ids)
                tasks.Add(DownloadVideoAsync(video_id, out_dir, out_file, out_error));

            Task.WaitAll([.. tasks]);
        }

        /// <summary>
        /// Attempts to extract the video ID from a provided youtube video URL. <para />
        /// Supported URL types: <para />
        /// - https://youtube.com/watch?v=&lt;VIDEO_ID&gt; <para />
        /// - https://youtube.com/embed/&lt;VIDEO_ID&gt; <para />
        /// - https://youtu.be/&lt;VIDEO_ID&gt; <para />
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        static string GetVideoId(string url)
        {
            var uri = new Uri(url);
            string? video_id = null;

            if (uri.Host.Contains("youtube.com"))
            {
                if (uri.AbsolutePath == "/watch") // youtube.com/watch?v=<VIDEO_ID>
                {
                    var veq_index = uri.Query.IndexOf("v=");
                    if (veq_index == -1)
                        veq_index = uri.Query.IndexOf("V=");

                    if (veq_index >= 0)
                    {
                        video_id = uri.Query[(veq_index + 2)..];
                    }
                } else if (uri.AbsolutePath.StartsWith("/embed")) // youtube.com/embed/<VIDEO_ID>
                {
                    video_id = uri.AbsolutePath[7..];
                }
            } else if(uri.Host.Contains("youtu.be")) // youtu.be/<VIDEO_ID>
            {
                video_id = uri.AbsolutePath[1..];
            }

            if (video_id == null)
                throw new Exception("No video ID found.");

            if (video_id.Contains('&'))
                video_id = video_id[..video_id.IndexOf('&')];
            if (video_id.Contains('?'))
                video_id = video_id[..video_id.IndexOf('?')];

            return video_id;
        }

        /// <summary>
        /// Initiates the download process for the video with the video id of <paramref name="video_id"/>.
        /// </summary>
        /// <param name="video_id">ID of the video to download</param>
        /// <param name="out_dir">Output directory</param>
        /// <param name="out_file">Output</param>
        /// <param name="out_error"></param>
        /// <returns></returns>
        static async Task DownloadVideoAsync(string video_id, string out_dir, string out_file, string out_error)
        {
            out_file = out_file.Replace("{video.id}", video_id);
            out_error = out_error.Replace("{video.id}", video_id);
            Console.WriteLine($"Looking up video '{video_id}'...");

            try
            {
                var video_url = $"{EMBED}/{video_id}";
                var youtube = new CustomYouTube();

                var videos = await youtube.GetAllVideosAsync(video_url);
                var audio = videos.Where(video => video.AudioBitrate > 0 && video.AudioFormat == AudioFormat.Aac).OrderByDescending(video => video.AudioBitrate).ThenBy(video => video.ContentLength).First();
                var video = videos.Where(video => video.Resolution <= 1080 && video.Format == VideoFormat.Mp4).OrderByDescending(video => video.Resolution).ThenByDescending(video => video.Fps).ThenBy(video => video.ContentLength).First();

                // Make sure output directory exists
                if (!Directory.Exists(out_dir))
                    Directory.CreateDirectory(out_dir);


                var audio_file = $"{out_dir}/{video_id}{GetExtension(audio.AudioFormat)}";
                var video_file = $"{out_dir}/{video_id}{GetExtension(video.Format)}";
                var final_file = $"{out_dir}/{string.Join("_", out_file.Replace("{video.id}", video_id).Replace("{video.title}", video.Title).Split(Path.GetInvalidFileNameChars()))}{GetExtension(video.Format)}";

                // Start downloading the audio
                await DownloadYouTubeData(youtube, audio, video_id, audio_file);
                Console.WriteLine();

                // Start downloading the video
                await DownloadYouTubeData(youtube, video, video_id, video_file);
                Console.WriteLine();

                Console.WriteLine($"Video '{video_id}' downloaded as audio and video! Now combining them with ffmpeg...");
                var args = string.Join(' ', [
                    "-y",
                    "-i", $"\"{video_file}\"",
                    "-i", $"\"{audio_file}\"",
                    "-acodec", "copy",
                    "-vcodec", "copy",
                    $"\"{final_file}\""
                ]);

                await Execute(FFMPEG_EXE, args);
                

                Console.WriteLine($"Video/audio combined for video '{video_id}'... Cleaning up.");

                File.Delete(audio_file);
                File.Delete(video_file);

                Console.WriteLine($"Video complete!: '{video_id} - {video.Title}'");

            } catch (Exception exception)
            {
                Console.WriteLine($"!! ERROR: Error downloading video '{video_id}' !!");
                var file_path = $"{out_dir}/{out_error}";
                Console.WriteLine($"Writing exception to: '{file_path}'...");
                File.WriteAllText(file_path, JsonConvert.SerializeObject(exception));
            }
        }

        static async Task Execute(string exe, string args)
        {
            using Process p = new();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = exe;
            p.StartInfo.Arguments = args;
            p.Start();

            p.OutputDataReceived += (sender, args) => Console.Write(args.Data);

            await p.WaitForExitAsync();
        }

        static string GetExtension(AudioFormat format)
            => format switch
            {
                AudioFormat.Aac => ".m4a",
                AudioFormat.Opus => ".webm",
                AudioFormat.Vorbis => ".ogg",
                _ => ".audio_unk"
            };

        static string GetExtension(VideoFormat format)
            => format switch
            {
                VideoFormat.Mp4 => ".mp4",
                VideoFormat.WebM => ".webm",
                _ => ".audio_unk"
            };


        /// <summary>
        /// Initiaties downloads and reports their progress to the console. <para />
        /// Warning: The progress reporting is a little finnicky if multiple downloads are occurring at once
        /// </summary>
        /// <param name="youtube">YouTube downloader client</param>
        /// <param name="video">Video data</param>
        /// <param name="video_id">ID of the video</param>
        /// <param name="file_path">Output file path</param>
        /// <returns>Nothing</returns>
        static async Task DownloadYouTubeData(CustomYouTube youtube, YouTubeVideo video, string video_id, string file_path)
        {
            await youtube.DownloadAsync(new Uri(video.Uri), $"{file_path}", new Progress<Tuple<long, long>>(tup => {
                var (progress, length) = tup;

                var percent = ((double)progress / length);
                var progress_mb = (progress / (double)(1024 * 1024)).ToString("N");
                var length_mb = (length / (double)(1024 * 1024)).ToString("N");
                Console.Write($" -  Downloading file for '{video_id}'... ( {percent,7:P2} ) {progress_mb} / {length_mb} MB\r");
            }));
        }
    }
}
