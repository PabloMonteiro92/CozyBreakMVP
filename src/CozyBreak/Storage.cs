using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CozyBreak;

public sealed class PerfilStore
{
    private readonly string _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CozyBreak");
    private string PathProtegido => System.IO.Path.Combine(_directory, "perfil.json.dpapi");
    private string PathLegado => System.IO.Path.Combine(_directory, "perfil.json");
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CozyBreak.profile.v1");

    public bool Existe => File.Exists(PathProtegido) || File.Exists(PathLegado);

    public Perfil Carregar()
    {
        try
        {
            if (File.Exists(PathProtegido))
            {
                var protegido = File.ReadAllBytes(PathProtegido);
                var json = ProtectedData.Unprotect(protegido, Entropy, DataProtectionScope.CurrentUser);
                var perfil = JsonSerializer.Deserialize<Perfil>(json, Options) ?? new Perfil();
                return Validacao.Perfil(perfil) is null ? perfil : new Perfil();
            }

            // Migração única de versões anteriores que usavam JSON sem proteção.
            if (File.Exists(PathLegado))
            {
                var perfil = JsonSerializer.Deserialize<Perfil>(File.ReadAllText(PathLegado), Options) ?? new Perfil();
                if (Validacao.Perfil(perfil) is null) { Salvar(perfil); TryDelete(PathLegado); return perfil; }
            }
        }
        catch (CryptographicException) { }
        catch (IOException) { }
        catch (JsonException) { }
        return new Perfil();
    }

    public void Salvar(Perfil perfil)
    {
        var error = Validacao.Perfil(perfil);
        if (error is not null) throw new ArgumentException(error);
        Directory.CreateDirectory(_directory);
        var json = JsonSerializer.SerializeToUtf8Bytes(perfil, Options);
        var protegido = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
        var temporary = PathProtegido + ".tmp";
        File.WriteAllBytes(temporary, protegido);
        if (File.Exists(PathProtegido)) File.Replace(temporary, PathProtegido, null);
        else File.Move(temporary, PathProtegido);
    }

    public void Apagar()
    {
        TryDelete(PathProtegido);
        TryDelete(PathLegado);
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } }
}
