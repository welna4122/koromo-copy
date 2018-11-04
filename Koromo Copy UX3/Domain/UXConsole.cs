﻿/***

   Copyright (C) 2018. dc-koromo. All Rights Reserved.
   
   Author: Koromo Copy Developer

***/

using Koromo_Copy.Console;
using Koromo_Copy.Interface;
using System.Windows;

namespace Koromo_Copy_UX3.Domain
{
    public class UXConsoleOption : IConsoleOption
    {
        [CommandLine("--help", CommandType.OPTION, Default = true)]
        public bool Help;

        [CommandLine("-window", CommandType.ARGUMENTS, Help = "use -window <Code>")]
        public string[] Window;
    }
    
    public class UXConsole : ILazy<UXConsole>, IConsole
    {
        public static void Register()
        {
            Console.Instance.redirections.Add("ux", new UXConsole());
        }

        static bool Redirect(string[] arguments, string contents)
        {
            UXConsoleOption option = CommandLineParser<UXConsoleOption>.Parse(arguments);

            if (option.Error)
            {
                Console.Instance.WriteLine(option.ErrorMessage);
                if (option.HelpMessage != null)
                    Console.Instance.WriteLine(option.HelpMessage);
                return false;
            }
            else if (option.Help)
            {
                PrintHelp();
            }
            else if (option.Window != null)
            {
                ProcessWindow(option.Window);
            }

            return true;
        }

        bool IConsole.Redirect(string[] arguments, string contents)
        {
            return Redirect(arguments, contents);
        }

        static void PrintHelp()
        {
            Console.Instance.WriteLine(
                "UX Console Core\r\n" +
                "\r\n" +
                " -window <Code> : Open specific window.\r\n"
                );
        }

        /// <summary>
        /// 새로운 창을 실행합니다.
        /// </summary>
        /// <param name="args"></param>
        static void ProcessWindow(string[] args)
        {
            switch (args[0])
            {
                case "artist_viewer":

                    Application.Current.Dispatcher.BeginInvoke(new System.Action(
                    delegate
                    {
                        ArtistViewerWindow avw = new ArtistViewerWindow();
                        avw.Show();
                    }));
                    break;

                default:
                    Console.Instance.WriteLine($"'{args[0]}' window is not found.");
                    break;
            }
        }
    }
}
