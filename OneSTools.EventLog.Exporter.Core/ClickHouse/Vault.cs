using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.AppRole;
using VaultSharp.V1.Commons;
using Microsoft.Extensions.Configuration;

namespace OneSTools.EventLog.Exporter.Core.ClickHouse
{
    public class Vault
    {
        public string[] GetSecretWithAppRole(IConfiguration configuration)
        {
            string vaultAddr = configuration.GetValue("Vault:VaultAddr","");
            string path = configuration.GetValue("Vault:Path","");
            string mountPoint = configuration.GetValue("Vault:MountPoint","");
            string roleId = configuration.GetValue("Vault:RoleId","");
            string secretId = configuration.GetValue("Vault:SecreteId","");
            string login = configuration.GetValue("Vault:Login","");
            string password = configuration.GetValue("Vault:Password","");

            IAuthMethodInfo authMethod = new AppRoleAuthMethodInfo(roleId, secretId.ToString());
            var vaultClientSettings = new VaultClientSettings(vaultAddr, authMethod);

            IVaultClient vaultClient = new VaultClient(vaultClientSettings);

            Secret<SecretData> kv2Secret = null;
            kv2Secret = vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(path:path , mountPoint: mountPoint).Result;

            var arr = new string[2];
            string username = kv2Secret.Data.Data[login].ToString();
            string pass = kv2Secret.Data.Data[password].ToString();
            arr[0] = username;
            arr[1] = pass;
            return arr;
        }
    }
}
