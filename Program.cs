﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LibHac;
using System.Linq;
using LibHac.IO;

namespace Quick_NCA_Finder
{
    class Program
    {
        static SwitchFs fs;
        static readonly DirectoryInfo NCAfolder = new DirectoryInfo("./NCAs");

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: Quick-NCA-Finder.exe {Folder to search, make this the root of the NAND partition or SD.} {TID or name of application to search for, use * for all titles, or leave blank to list all titles.} {add `NSP` to pack into NSP or keep blank for no NSP");
                return;
            }

            DirectoryInfo dir = new DirectoryInfo(args[0]);
            FileInfo prodkeys = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch/prod.keys"));
            FileInfo titlekeys = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch/title.keys"));
            FileInfo consolekeys = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch/console.keys"));
            int i = 0;

            if (!prodkeys.Exists || !titlekeys.Exists || !consolekeys.Exists)
            {
                Console.WriteLine("Your prod.keys, title.keys or console.keys do not exist at ~/.switch/ derive them with HACGUI or place them there.");
                return;
            }

            Keyset keys = new Keyset();
            keys = ExternalKeys.ReadKeyFile(prodkeys.FullName, titlekeys.FullName, consolekeys.FullName);
            fs = new SwitchFs(keys, new FileSystem(dir.FullName));

            if (args.Length == 1)
            {
                foreach (KeyValuePair<ulong, Title> kv in fs.Titles)
                {
                    ulong titleId = kv.Key;
                    Title title = kv.Value;
                    Console.WriteLine($"{titleId:X8} {title.Name}");
                }
                Console.WriteLine("Done!");
                return;
            }

            if (args[1] == "*" && args[2].ToLower() == "nsp")
            {
                foreach (KeyValuePair<ulong, Title> kv in fs.Titles)
                {
                    ulong titleId = kv.Key;
                    Title title = kv.Value;
                    string titleRoot = $"{titleId:X8} [{title.Name}] [v{title.Version}]";
                    string safeNSPName = new string(titleRoot.Where(c => !Path.GetInvalidPathChars().Contains(c)).ToArray()); //remove unsafe chars
                    safeNSPName = safeNSPName.Replace(":", ""); //manually remove `:` cuz mircosoft doesnt have it in their list
                    FileInfo SafeNSP = new FileInfo(Path.Combine(NCAfolder.FullName, safeNSPName));
                    Pfs0Builder NSP = new Pfs0Builder();

                    foreach (Nca nca in title.Ncas)
                    {
                        NSP.AddFile(nca.NcaId, nca.GetStorage().AsStream());
                        Console.WriteLine($"Saving {nca.Header.TitleId:X8} {title.Name}: {nca.Header.ContentType} to working PFS0...");
                    }
                    using (FileStream dest = SafeNSP.Create())
                    {
                        NSP.Build(dest, new ProgressBar());
                    }
                }
                Console.WriteLine("Done!");
                return;
            }

            if (args[1] == "*" && args[2].ToLower() != "nsp")
            {
                foreach (KeyValuePair<ulong, Title> kv in fs.Titles)
                {
                    ulong titleId = kv.Key;
                    Title title = kv.Value;
                    string titleRoot = $"{titleId:X8} {title.Name}";
                    string safeDirectoryName = new string(titleRoot.Where(c => !Path.GetInvalidPathChars().Contains(c)).ToArray()); //remove unsafe chars
                    safeDirectoryName = safeDirectoryName.Replace(":", ""); //manually remove `:` cuz mircosoft doesnt have it in their list
                    DirectoryInfo safeDirectory = new DirectoryInfo(Path.Combine(NCAfolder.FullName, safeDirectoryName));
                    safeDirectory.Create();

                    foreach (Nca nca in title.Ncas)
                    {
                        Console.WriteLine($"Saving {nca.Header.TitleId:X8} {title.Name}: {nca.Header.ContentType} to working directory...");
                        FileInfo ncainfo = safeDirectory.GetFile($"{nca.Header.ContentType}00.nca");
                        if (ncainfo.Exists)
                        {
                            i++;
                            ncainfo = safeDirectory.GetFile($"{nca.Header.ContentType}0{i}.nca");
                        }
                        else i = 0;
                        using (Stream source = nca.GetStorage().AsStream())
                        using (FileStream dest = ncainfo.Create())
                        {
                            source.CopyTo(dest);
                        }
                    }
                }
                Console.WriteLine("Done!");
                return;
            }

            ulong TID;
            try
            {
                TID = ulong.Parse(args[1], NumberStyles.HexNumber);
            }
            catch
            {
                if (args[2].ToLower() != "nsp") GetNCAs(true, TitleName: args[1]);
                else GetNSP(true, TitleName: args[1]);
                return;
            }
            if (args[2].ToLower() != "nsp") GetNCAs(false, TID: TID);
            else GetNSP(false, TID: TID);
        }

        private static void GetNCAs(bool SearchByName, ulong TID = 0, string TitleName = null)
        {
            int i = 0;
            if (SearchByName)
            {
                foreach (KeyValuePair<ulong, Title> kv in fs.Titles)
                {
                    ulong titleId = kv.Key;
                    Title title = kv.Value;

                    if (title.Name.ToLower() == TitleName.ToLower() || title.Name.ToLower().Contains(TitleName.ToLower()))
                    {
                        Console.WriteLine("Found!");
                        string titleRoot = $"{titleId:X8} {title.Name}";
                        string safeDirectoryName = new string(titleRoot.Where(c => !Path.GetInvalidPathChars().Contains(c)).ToArray()); //remove unsafe chars
                        safeDirectoryName = safeDirectoryName.Replace(":", ""); //manually remove `:` cuz mircosoft doesnt have it in their list
                        DirectoryInfo safeDirectory = new DirectoryInfo(Path.Combine(NCAfolder.FullName, safeDirectoryName));
                        safeDirectory.Create();

                        foreach (Nca nca in title.Ncas)
                        {
                            Console.WriteLine($"Saving {nca.Header.TitleId:X8} {title.Name}: {nca.Header.ContentType} to working directory...");
                            FileInfo ncainfo = safeDirectory.GetFile($"{nca.Header.ContentType}00.nca");
                            if (ncainfo.Exists)
                            {
                                i++;
                                ncainfo = safeDirectory.GetFile($"{nca.Header.ContentType}0{i}.nca");
                            }
                            else i = 0;
                            using (Stream source = nca.GetStorage().AsStream())
                            using (FileStream dest = ncainfo.Create())
                            {
                                source.CopyTo(dest);
                            }
                        }
                        Console.WriteLine("Done!");
                        return;
                    }
                    else Console.WriteLine($"{titleId:X8}");
                }
                Console.WriteLine("TID not found!");
            }
            else
            {
                foreach (KeyValuePair<ulong, Title> kv in fs.Titles)
                {
                    ulong titleId = kv.Key;
                    Title title = kv.Value;


                    if (titleId == TID)
                    {
                        Console.WriteLine("Found!");
                        string titleRoot = $"{titleId:X8} {title.Name}";
                        string safeDirectoryName = new string(titleRoot.Where(c => !Path.GetInvalidPathChars().Contains(c)).ToArray()); //remove unsafe chars
                        safeDirectoryName = safeDirectoryName.Replace(":", ""); //manually remove `:` cuz mircosoft doesnt have it in their list
                        DirectoryInfo safeDirectory = new DirectoryInfo(Path.Combine(NCAfolder.FullName, safeDirectoryName));
                        safeDirectory.Create();

                        foreach (Nca nca in title.Ncas)
                        {
                            Console.WriteLine($"Saving {nca.Header.TitleId:X8} {title.Name}: {nca.Header.ContentType} to working directory...");
                            FileInfo ncainfo = safeDirectory.GetFile($"{nca.Header.ContentType}00.nca");
                            if (ncainfo.Exists)
                            {
                                i++;
                                ncainfo = safeDirectory.GetFile($"{nca.Header.ContentType}0{i}.nca");
                            }
                            else i = 0;
                            using (Stream source = nca.GetStorage().AsStream())
                            using (FileStream dest = ncainfo.Create())
                            {
                                source.CopyTo(dest);
                            }
                        }
                        Console.WriteLine("Done!");
                        return;
                    }
                    else Console.WriteLine($"{titleId:X8}");
                }
                Console.WriteLine("TID not found!");
            }
        }
        private static void GetNSP(bool SearchByName, ulong TID = 0, string TitleName = null)
        {
            Pfs0Builder NSP = new Pfs0Builder();
            if (SearchByName)
            {
                foreach (KeyValuePair<ulong, Title> kv in fs.Titles)
                {
                    ulong titleId = kv.Key;
                    Title title = kv.Value;

                    if (title.Name.ToLower() == TitleName.ToLower() || title.Name.ToLower().Contains(TitleName.ToLower()))
                    {
                        string titleRoot = $"{titleId:X8} [{title.Name}] [v{title.Version}]";
                        string safeNSPName = new string(titleRoot.Where(c => !Path.GetInvalidPathChars().Contains(c)).ToArray()); //remove unsafe chars
                        safeNSPName = safeNSPName.Replace(":", ""); //manually remove `:` cuz mircosoft doesnt have it in their list
                        FileInfo SafeNSP = new FileInfo(Path.Combine(NCAfolder.FullName, safeNSPName));

                        foreach (Nca nca in title.Ncas)
                        {
                            NSP.AddFile(nca.NcaId, nca.GetStorage().AsStream());
                            Console.WriteLine($"Saving {nca.Header.TitleId:X8} {title.Name}: {nca.Header.ContentType} to PFS0...");
                        }
                        using (FileStream dest = SafeNSP.Create())
                        {
                            NSP.Build(dest, new ProgressBar());
                        }
                        Console.WriteLine("Done!");
                        return;
                    }
                    else Console.WriteLine($"{titleId:X8}");
                }
                Console.WriteLine("TID not found!");
            }
            else
            {
                foreach (KeyValuePair<ulong, Title> kv in fs.Titles)
                {
                    ulong titleId = kv.Key;
                    Title title = kv.Value;

                    if (titleId == TID)
                    {
                        string titleRoot = $"{titleId:X8} [{title.Name}] [v{title.Version}]";
                        string safeNSPName = new string(titleRoot.Where(c => !Path.GetInvalidPathChars().Contains(c)).ToArray()); //remove unsafe chars
                        safeNSPName = safeNSPName.Replace(":", ""); //manually remove `:` cuz mircosoft doesnt have it in their list
                        FileInfo SafeNSP = new FileInfo(Path.Combine(NCAfolder.FullName, safeNSPName));


                        foreach (Nca nca in title.Ncas)
                        {
                            NSP.AddFile(nca.NcaId, nca.GetStorage().AsStream());
                            Console.WriteLine($"Saving {nca.Header.TitleId:X8} {title.Name}: {nca.Header.ContentType} to PFS0...");
                        }
                        using (FileStream dest = SafeNSP.Create())
                        {
                            NSP.Build(dest, new ProgressBar());
                        }
                        Console.WriteLine("Done!");
                        return;
                    }
                    else Console.WriteLine($"{titleId:X8}");
                }
                Console.WriteLine("TID not found!");
            }
        }
    }
}
