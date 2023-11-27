using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Globalization;
using System.Threading;
using System.Runtime.InteropServices;
using System.Reflection;
using Microsoft.VisualBasic.ApplicationServices;
using Toolbox.Library;
using FirstPlugin;
using CafeLibrary.M2;
using Bfres.Structs;
using Toolbox.Library.Animations;

namespace Toolbox
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Toolbox.Library.Runtime.ExecutableDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

            List<string> Args = new List<string>();
            foreach (var arg in args)
            {
                if (arg != Application.ExecutablePath)
                    Args.Add(arg);
            }

            if(Args.Count < 1)
            {
                Console.WriteLine("Usage: toolbox.exe <action> <args>");
                return 1;
            }

            String action = Args[0];
            Args.RemoveAt(0);
            switch(action)
            {
                case "model":
                    {
                        if (Args.Count != 3)
                        {
                            Console.WriteLine("Usage: toolbox.exe model <src.bfres.zs> <name> <dest.dae> ; got: " + Args.Count + " args");
                            return 1;
                        }

                        byte[] decomp = Zstb.SDecompress(File.ReadAllBytes(Args[0]));
                        BFRES bfres = new BFRES();
                        bfres.LoadFile(new Syroot.NintenTools.NSW.Bfres.ResFile(new MemoryStream(decomp)));
                        foreach (Bfres.Structs.FMDL model in bfres.GetModels())
                        {
                            if (model.Model.Name.Equals(Args[1]))
                            {
                                model.ExportSilent(Args[2]);
                                Console.WriteLine("Found, successfully converted!");
                                return 0;
                            }
                        }
                        Console.WriteLine("Couldn't find model named " + Args[1]);
                        return 1;
                    }
                case "anim":
                    {
                        if (Args.Count != 3)
                        {
                            Console.WriteLine("Usage: toolbox.exe model <src.bfres.zs> <name> <dest.smd> ; got: " + Args.Count + " args");
                            return 1;
                        }

                        // load BFRES file
                        byte[] decomp = Zstb.SDecompress(File.ReadAllBytes(Args[0]));
                        BFRES bfres = new BFRES();
                        bfres.BFRESRender = new BFRESRender();
                        bfres.LoadFile(new Syroot.NintenTools.NSW.Bfres.ResFile(new MemoryStream(decomp)));

                        // prepare BFRESRender to make auto-selecting skeleton for animation possible
                        var Models = bfres.GetModels();
                        if (Models != null)
                        {
                            foreach (FMDL mdl in Models)
                            {
                                bfres.BFRESRender.models.Add(mdl);
                            }
                        }

                        // search and export animation
                        foreach (Bfres.Structs.FSKA anim in bfres.GetSkeletalAnims())
                        {
                            if (anim.SkeletalAnim.Name.Equals(Args[1]))
                            {
                                anim.Export(Args[2]);
                                Console.WriteLine("Found, successfully converted!");
                                return 0;
                            }
                        }
                        Console.WriteLine("Couldn't find animation named " + Args[1]);
                        return 1;
                    }
                default:
                    Console.WriteLine("Unknown action: " + action);
                    return 1;
            }

        }

        [ComVisible(true), ComImport, Guid("000214eb-0000-0000-c000-000000000046"),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IExtractIcon
        {
            [PreserveSig]
            uint GetIconLocation(int uFlags, IntPtr szIconFile, int cchMax, IntPtr piIndex, UIntPtr pwFlags);

            [PreserveSig]
            uint Extract(string pszFile, uint nIconIndex, ref IntPtr phiconLarge, ref IntPtr phiconSmall, uint nIconSize);
        }

        public class SingleInstanceController : WindowsFormsApplicationBase
        {
            public SingleInstanceController()
            {
                IsSingleInstance = true;
                Startup += OnStart;
                StartupNextInstance += Program_StartupNextInstance;
            }

            private void OnStart(object sender, StartupEventArgs e)
            {
                List<string> args = new List<string>();
                foreach (string arg in e.CommandLine)
                    args.Add(arg);

                args.RemoveAt(0);

                Toolbox.MainForm.Instance.OpenedFiles = args;
            }

            void Program_StartupNextInstance(object sender, StartupNextInstanceEventArgs e)
            {
                e.BringToForeground = true;
                MainForm form = MainForm as MainForm;
                form.OpenedFiles = e.CommandLine.ToList();
                form.OpenFiles();
            }

            protected override void OnCreateMainForm()
            {
                MainForm = Toolbox.MainForm.Instance;
            }
        }

        private static bool TryLoadZSTD()
        {
            try
            {
                String folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                String filePath = Path.Combine(folder, Environment.Is64BitProcess ? "x64" : "x86", "libzstd.dll");
                if (File.Exists(filePath))
                {
                    Assembly assembly = Assembly.LoadFile(filePath);
                    return true;
                }
            }
            catch (Exception ex)
            {
            }
            return false;
        }

        private static bool TryLoadDirectXTex()
        {
            try
            {
                String folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                String filePath = Path.Combine(folder, Environment.Is64BitProcess ? "x64" : "x86", "DirectXTexNetImpl.dll");
                String filePathLib = Path.Combine(folder, "Lib", Environment.Is64BitProcess ? "x64" : "x86", "DirectXTexNetImpl.dll");
                if (File.Exists(filePath))
                {
                    Assembly assembly = Assembly.LoadFile(filePath);
                    return true;
                }
                if (File.Exists(filePathLib))
                {
                    Assembly assembly = Assembly.LoadFile(filePathLib);
                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            return false;
        }

        /// 
        /// Include externals dlls
        /// 
        private static Assembly LoadAssembly(object sender, ResolveEventArgs args)
        {
            Assembly result = null;
            if (args != null && !string.IsNullOrEmpty(args.Name))
    {
                //Get current exe fullpath
                FileInfo info = new FileInfo(Assembly.GetExecutingAssembly().Location);

                //Get folder of the executing .exe
                var folderPath = Path.Combine(info.Directory.FullName, "Lib");

                //Build potential fullpath to the loading assembly
                var assemblyName = args.Name.Split(new string[] { "," }, StringSplitOptions.None)[0];
                var assemblyExtension = "dll";
                var assemblyPath = Path.Combine(folderPath, string.Format("{0}.{1}", assemblyName, assemblyExtension));

                //Check if the assembly exists in our "Libs" directory
                if (File.Exists(assemblyPath))
                {
                    Console.WriteLine("Loading .dll " + assemblyPath);

                    //Load the required assembly using our custom path
                    result = Assembly.LoadFrom(assemblyPath);
                }
                else
                {
                    //Keep default loading
                    return args.RequestingAssembly;
                }
            }

            return result;
        }

        // Custom assembly resolver to find the architecture-specific implementation assembly.
        private static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("DirectXTexNetImpl", StringComparison.OrdinalIgnoreCase))
            {
                var assembly = Assembly.GetExecutingAssembly();
                var path = GetAssemblyPath(Path.GetDirectoryName(assembly.Location));

                if (!File.Exists(path))
                {
                    // If we can't find the file, try using the CodeBase instead (which can
                    // be different if using shadow copy).
                    // CodeBase is a uri, so must parse out the filename.
                    path = GetAssemblyPath(Path.GetDirectoryName(new Uri(assembly.CodeBase).AbsolutePath));
                }

                return Assembly.LoadFile(path);
            }

            return null;
        }

        private static string GetAssemblyPath(string dir) => Path.Combine(dir,"Lib", ArchitectureMoniker, "DirectXTexNetImpl.dll");

        // Note: currently no support for ARM.
        // Don't use %PROCESSOR_ARCHITECTURE% as it calls x64 'AMD64'.
        private static string ArchitectureMoniker => Environment.Is64BitProcess ? "x64" : "x86";
    }
}
