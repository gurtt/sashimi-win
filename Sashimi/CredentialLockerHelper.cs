using System;
using Windows.Security.Credentials;

public class CredentialLockerHelper
{
    private static readonly string resource = "Sashimi";
    private static readonly PasswordVault vault = new();

    /// <summary>
    /// Stores the string in the credential locker under the given key.
    /// </summary>
    /// <param name="key">The key under which the data is stored in the credential locker.</param>
    /// <param name="value">The values to be written to the credential locker.</param>
    /// <exception cref="Exception">If the item isn't added successfully.</exception>
    public static void Set(string key, string value)
    {
        vault.Add(new PasswordCredential(resource, key, value));
    }

    /// <summary>
    /// Retrieves the text value from the credential locker under the given key.
    /// </summary>
    /// <param name="key">The key under which the data is stored in the credential locker.</param>
    /// <exception cref="Exception">If the item isn't retrieved successfully.</exception>
    public static string Get(string key)
    {
        PasswordCredential credential = vault.Retrieve(resource, key);
        credential.RetrievePassword();
        return credential.Password;
    }

    /// <summary>
    /// Deletes the credential in the credential locker under the given key.
    /// </summary>
    /// <param name="key">The key under which the data is stored in the credential locker.</param>
    /// <exception cref="Exception">If the item isn't deleted successfully.</exception>
    /// 
    public static void Remove(string key)
    {
        PasswordCredential credential = vault.Retrieve(resource, key);
        vault.Remove(credential);
    }

}
