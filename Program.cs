using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SimpleConcater
{
	class Program
	{
		static readonly Regex FILE_NAMING_PATTERN = new Regex(@"GX(\d{2})(\d{4}).MP4");

		static async Task Main(string[] args)
		{
			var directory = args.First();
			var videos = Directory.GetFiles(directory, "*.mp4")
				.Select(name =>
				{
					var match = FILE_NAMING_PATTERN.Match(name);
					return (name: name, group: match.Groups[2].Value, index: match.Groups[1].Value);
				})
				.GroupBy(f => f.group)
				.Select(fileGroup =>
				{
					var recipe = Path.GetTempFileName();
					var list = fileGroup
						.OrderBy(f => f.index)
						.Select(f => $"file {f.name.Replace(@"\", @"\\")}")
						.ToArray();
					File.WriteAllLines(recipe, list);
					var date = File.GetLastWriteTime(fileGroup.First().name);
					var groupNo = fileGroup.First().group;
					var name = Path.Combine(directory, $"{date:yyyy_MM_dd_hh_mm}_{groupNo}.mp4");
					var files = fileGroup.Select(f => Path.Combine(directory, f.name));
					return (groupNo, name, recipe, date, list, files);
				})
				.ToArray();

			foreach (var video in videos)
			{
				var process = new Process()
				{
					StartInfo = new ProcessStartInfo()
					{
						FileName = "ffmpeg",
						Arguments = $"-f concat -safe 0 -i {video.recipe} -c copy {video.name}",
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
						WorkingDirectory = directory,
					},
				};
				process.Start();
				var stdoutTask = Task.Run(() =>
				{
					while(!process.StandardOutput.EndOfStream)
					{
						Console.WriteLine(process.StandardOutput.ReadLine());
					}
				});
				var stderrorTask = Task.Run(() =>
				{
					while(!process.StandardError.EndOfStream)
					{
						Console.WriteLine(process.StandardError.ReadLine());
					}
				});
				await Task.WhenAll(stdoutTask, stderrorTask);
				process.WaitForExit();
				File.Delete(video.recipe);

				var trash = Path.Combine(directory, "trash");
				Directory.CreateDirectory(trash);
				foreach (var old in video.files)
				{
					File.Move(old, Path.Combine(trash, Path.GetFileName(old)));
				}
			}

			// Console.ReadKey();
		}
	}
}
