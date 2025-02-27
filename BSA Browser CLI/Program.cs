﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BSA_Browser_CLI.Filtering;
using SharpBSABA2;
using SharpBSABA2.BA2Util;

namespace BSA_Browser_CLI
{
    class Program
    {
        private const int ERROR_INVALID_FUNCTION = 1;
        private const int ERROR_FILE_NOT_FOUND = 2;
        private const int ERROR_PATH_NOT_FOUND = 3;
        private const int ERROR_BAD_ARGUMENTS = 160;

        static Arguments _arguments;
        static List<IFilterPredicate> _filters = new List<IFilterPredicate>();

        static void Main(string[] args)
        {
            // Parse arguments. Go to exit if null, errors has occurred and been handled
            if ((_arguments = ParseArguments(args)) == null)
                goto exit;

            // Print help screen. Ignore other arguments
            if (args.Length == 0 || _arguments.Help)
            {
                PrintHelp();
                goto exit;
            }

            if (_arguments.Inputs.Count == 0)
            {
                Console.WriteLine("No input file(s) found");
                Environment.ExitCode = ERROR_FILE_NOT_FOUND;
                goto exit;
            }

            // Setup filters
            foreach (var filter in _arguments.Filters)
            {
                switch (filter.Type)
                {
                    case FilteringTypes.Simple:
                        _filters.Add(new FilterPredicateSimple(filter.Pattern));
                        break;
                    case FilteringTypes.SimpleExclude:
                        _filters.Add(new FilterPredicateSimpleExclude(filter.Pattern));
                        break;
                    case FilteringTypes.Regex:
                        try
                        {
                            _filters.Add(new FilterPredicateRegex(filter.Pattern));
                        }
                        catch
                        {
                            Console.WriteLine("Invalid regex filter string");
                            Environment.ExitCode = ERROR_BAD_ARGUMENTS;
                            goto exit;
                        }
                        break;
                    default:
                        throw new Exception("Unknown filter type: " + filter.Type);
                }
            }

            // Default to list if no other options have been given
            if (_arguments.List || (!_arguments.List && !_arguments.Extract && !_arguments.Help))
            {
                try
                {
                    PrintFileList(_arguments.Inputs.ToList(), _arguments.ListOptions);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occured opening archive:");
                    Console.WriteLine(ex.Message);
                    Environment.ExitCode = ERROR_INVALID_FUNCTION;
                }
            }

            if (_arguments.Extract)
            {
                try
                {
                    ExtractFiles(_arguments.Inputs.ToList(), _arguments.Destination, _arguments.Overwrite);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occured opening archive:");
                    Console.WriteLine(ex.Message);
                    Environment.ExitCode = ERROR_INVALID_FUNCTION;
                }
            }

        exit:;

#if DEBUG
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
#endif
        }

        static Arguments ParseArguments(params string[] args)
        {
            try
            {
                return new Arguments(args);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                Environment.ExitCode = ERROR_BAD_ARGUMENTS;
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine("Input file not found: " + ex.FileName);
                Environment.ExitCode = ERROR_FILE_NOT_FOUND;
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine(ex.Message);
                Environment.ExitCode = ERROR_PATH_NOT_FOUND;
            }

            return null;
        }

        static void PrintHelp()
        {
            Console.WriteLine("BSA Browser CLI - " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
            Console.WriteLine("Extract or list files inside .bsa and .ba2 archives.");
            Console.WriteLine();
            Console.WriteLine("bsab [OPTIONS] FILE [FILE...] [DESTINATION]");
            Console.WriteLine();
            Console.WriteLine("  -h, --help             Display this help page");
            Console.WriteLine("  -i                     Ignore errors with opening archives or extracting files");
            Console.WriteLine("  -e:[OPTIONS]           Extract files");
            Console.WriteLine("     options               N   Extract files directly into destination, without directories");
            Console.WriteLine("  -l:[OPTIONS]           List files");
            Console.WriteLine("     options               A   Prepend each line with archive filename");
            Console.WriteLine("                           F   Prepend each line with full archive file path");
            Console.WriteLine("                           N   Display filename only");
            Console.WriteLine("                           S   Display file size (bytes)");
            Console.WriteLine("                           X   Display file size (humanize)");
            Console.WriteLine("  -o, --overwrite        Overwrite existing files");
            Console.WriteLine("  -f FILTER              Simple filtering. Wildcard supported. Case-insensitive");
            Console.WriteLine("  --exclude FILTER       Exclude using simple filtering. Wildcard supported. Case-insensitive");
            Console.WriteLine("  --regex REGEX          Regex filtering. Case-sensitive");
            Console.WriteLine("  --encoding ENCODING    Set encoding to use");
            Console.WriteLine("     encodings             utf7     (Default)");
            Console.WriteLine("                           system   Use system default encoding");
            Console.WriteLine("                           ascii");
            Console.WriteLine("                           unicode");
            Console.WriteLine("                           utf32");
            Console.WriteLine("                           utf8");
            Console.WriteLine("  --noheaders            Extract unsupported textures without DDS header instead of skipping");
            Console.WriteLine("  --mtc                  Match time changed on extracted files with archive");
            Console.WriteLine();
            Console.WriteLine("Multiple filters can be defined and mixed. Filters are matched from first to last.");
            Console.WriteLine();
        }

        static void PrintFileList(List<string> archives, ListOptions options)
        {
            archives.ForEach(archivePath =>
            {
                // If there are multiple archives print archive filename to differentiate
                if (archives.Count > 1)
                    Console.WriteLine($"{Path.GetFileName(archivePath)}:");

                Archive archive = null;

                try
                {
                    archive = OpenArchive(archivePath);
                }
                catch (Exception)
                {
                    if (!_arguments.IgnoreErrors)
                        throw;
                    else
                        Console.WriteLine($"An error occured opening '{Path.GetFileName(archivePath)}'. Skipping...");
                }

                bool filename = options.HasFlag(ListOptions.Filename);
                bool filesize = options.HasFlag(ListOptions.FileSize);
                bool filesizeFormat = options.HasFlag(ListOptions.FileSizeFormat);
                string prefix = FormatPrefix(options, archive);
                string indent = string.IsNullOrEmpty(prefix) && archives.Count > 1 ? "\t" : string.Empty;

                foreach (var entry in archive.Files.Where(x => Filter(x.FullPath)))
                {
                    string filesizeString = filesizeFormat ? FormatBytes(Math.Max(entry.RealSize, entry.Size)).PadLeft(12) + "\t" :
                                                  filesize ? Math.Max(entry.RealSize, entry.Size).ToString("N0").PadLeft(12) + "\t" : string.Empty;

                    Console.WriteLine("{0}{1}{2}",
                        indent,
                        filesizeString,
                        Path.Combine(prefix, filename ? entry.FileName : entry.FullPath));
                }

                Console.WriteLine();
            });
        }

        static void ExtractFiles(List<string> archives, string destination, bool overwrite)
        {
            archives.ForEach(archivePath =>
            {
                Archive archive = null;

                try
                {
                    archive = OpenArchive(archivePath);
                }
                catch (Exception)
                {
                    if (!_arguments.IgnoreErrors)
                        throw;
                    else
                        Console.WriteLine($"An error occured opening '{Path.GetFileName(archivePath)}'. Skipping...");
                }

                int count = 0;
                int line = -1;
                int prevLength = 0;
                int skipped = 0;
                var files = archive.Files;//.Where(x => Filter(x.FullPath)).ToList();

                HandleUnsupportedTextures(files);

                // Some Console properties might not be available in certain situations, 
                // e.g. when redirecting stdout. To prevent crashing, setting the cursor position should only
                // be done if there actually is a cursor to be set.
                try
                {
                    line = Console.CursorTop;
                }
                catch (IOException) { }

                foreach (var entry in files)
                {
                    var output = $"Extracting: {++count}/{files.Count} - {entry.FullPath}".PadRight(prevLength);

                    if (line > -1)
                    {
                        Console.SetCursorPosition(0, line);
                        Console.Write(output);
                    }
                    else
                    {
                        Console.WriteLine(output);
                    }
                    prevLength = output.Length;

                    try
                    {
                        if (!overwrite && File.Exists(Path.Combine(destination, entry.FullPath)))
                        {
                            skipped++;
                        }
                        else
                        {
                            entry.Extract(destination, _arguments.ExtractOptions.HasFlag(ExtractOptions.Directory));
                        }
                    }
                    catch (Exception)
                    {
                        if (!_arguments.IgnoreErrors)
                            throw;
                        else
                            Console.WriteLine($"An error occured extracting '{entry.FullPath}'. Skipping...");
                    }
                }

                Console.WriteLine();

                if (skipped > 0)
                    Console.WriteLine($"Skipped {skipped} existing files");
            });
        }

        static bool Filter(string input)
        {
            foreach (var filter in _filters)
            {
                if (filter.Match(input) == false)
                    return false;
            }

            return true;
        }

        static Archive OpenArchive(string file)
        {
            Archive archive = null;
            string extension = Path.GetExtension(file);

            switch (extension.ToLower())
            {
                case ".bsa":
                case ".dat":
                    archive = new SharpBSABA2.BSAUtil.BSA(file, _arguments.Encoding);
                    break;
                case ".ba2":
                    archive = new SharpBSABA2.BA2Util.BA2(file, _arguments.Encoding);
                    break;
                default:
                    throw new Exception($"Unrecognized archive file type ({extension}).");
            }

            archive.MatchLastWriteTime = _arguments.MatchTimeChanged;
            archive.Files.Sort((a, b) => string.CompareOrdinal(a.LowerPath, b.LowerPath));
            return archive;
        }

        static void HandleUnsupportedTextures(List<ArchiveEntry> files)
        {
            for (int i = files.Count; i-- > 0;)
            {
                if (files[i] is BA2TextureEntry tex && tex.IsFormatSupported() == false)
                {
                    if (_arguments.NoHeaders)
                        tex.GenerateTextureHeader = false;
                    else
                        files.RemoveAt(i); // Remove unsupported textures to skip them
                }
            }
        }

        static string FormatBytes(long bytes)
        {
            const int scale = 1024;
            string[] orders = new string[] { "GB", "MB", "KB", " B" };
            long max = (long)Math.Pow(scale, orders.Length - 1);

            foreach (string order in orders)
            {
                if (bytes > max)
                    return string.Format("{0:#.00} {1}", decimal.Divide(bytes, max), order);

                max /= scale;
            }
            return "0 Bytes";
        }

        static string FormatPrefix(ListOptions options, Archive archive)
        {
            string prefix = string.Empty;

            if (options.HasFlag(ListOptions.Archive))
                prefix = Path.GetFileName(archive.FullPath);

            if (options.HasFlag(ListOptions.FullPath))
                prefix = Path.GetFullPath(archive.FullPath);
            return prefix;
        }
    }
}
