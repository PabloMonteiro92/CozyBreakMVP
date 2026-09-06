using System.IO;

namespace CozyBreak;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnDispatcherUnhandledException(object? sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogAndShow(e.Exception);
        e.Handled = true;
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogAndShow(ex);
    }

    private static void LogAndShow(Exception ex)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "CozyBreak-erro.txt");
            File.WriteAllText(path, ex.ToString());
        }
        catch
        {
            // Se não conseguir gravar o log, ainda assim mostramos a mensagem na tela.
        }

        System.Windows.MessageBox.Show(
            ex.ToString(),
            "CozyBreak — erro inesperado",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }
}
