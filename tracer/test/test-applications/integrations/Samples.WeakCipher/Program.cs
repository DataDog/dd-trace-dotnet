using System;
using System.IO;
using System.Security.Cryptography;

namespace Samples.WeakCipher;

internal static class Program
{
    private static void Main()
    {
        VulnerableSection();
        NotVulnerableSection();
    }

    private static void NotVulnerableSection()
    {
#pragma warning disable SYSLIB0021 // Type or member is obsolete
        // Not vulnerable section
#pragma warning disable SYSLIB0022 // Type or member is obsolete
        testSymmetricAlgorithm(Rijndael.Create());
        testSymmetricAlgorithm(new RijndaelManaged());
#pragma warning restore SYSLIB0022 // Type or member is obsolete
        testSymmetricAlgorithm(Aes.Create());
        testSymmetricAlgorithm(new AesCryptoServiceProvider());
#pragma warning restore SYSLIB0021 // Type or member is obsolete
    }

    private static void VulnerableSection()
    {
#pragma warning disable SYSLIB0021 // Type or member is obsolete
        // Vulnerable section
        //https://rules.sonarsource.com/csharp/type/Vulnerability/RSPEC-5547
        testSymmetricAlgorithm(DES.Create());
        testSymmetricAlgorithm(new DESCryptoServiceProvider());
        System.Threading.Thread.Sleep(100);
        testSymmetricAlgorithm(RC2.Create());
        testSymmetricAlgorithm(new RC2CryptoServiceProvider());
        System.Threading.Thread.Sleep(100);
        // https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca5350
        testSymmetricAlgorithm(TripleDES.Create());
        testSymmetricAlgorithm(new TripleDESCryptoServiceProvider());
#pragma warning restore SYSLIB0021 // Type or member is obsolete
    }

    private static void testSymmetricAlgorithm(SymmetricAlgorithm algorithm)
    {
        var original = "Here is some data to encrypt!";
        var encrypted = EncryptStringToBytes(original, algorithm);
        var roundtrip = DecryptStringFromBytes(encrypted, algorithm);

        //Display the original data and the decrypted data.
        Console.WriteLine("Original:   {0}", original);
        Console.WriteLine("Round Trip: {0}", roundtrip);
    }

    static byte[] EncryptStringToBytes(string plainText, SymmetricAlgorithm algorithm)
    {
        // Check arguments.
        if (plainText == null || plainText.Length <= 0)
            throw new ArgumentNullException("plainText");

        byte[] encrypted;

        // Create an encryptor to perform the stream transform.
        ICryptoTransform encryptor = algorithm.CreateEncryptor(algorithm.Key, algorithm.IV);

        // Create the streams used for encryption.
        using (MemoryStream msEncrypt = new MemoryStream())
        {
            using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            {
                using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                {
                    //Write all data to the stream.
                    swEncrypt.Write(plainText);
                }
                encrypted = msEncrypt.ToArray();
            }
        }

        // Return the encrypted bytes from the memory stream.
        return encrypted;
    }

    static string DecryptStringFromBytes(byte[] cipherText, SymmetricAlgorithm algorithm)
    {
        // Check arguments.
        if (cipherText == null || cipherText.Length <= 0)
            throw new ArgumentNullException("cipherText");

        // Declare the string used to hold
        // the decrypted text.
        string plaintext = null;

        // Create a decryptor to perform the stream transform.
        ICryptoTransform decryptor = algorithm.CreateDecryptor(algorithm.Key, algorithm.IV);

        // Create the streams used for decryption.
        using (MemoryStream msDecrypt = new MemoryStream(cipherText))
        {
            using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
            {
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {

                    // Read the decrypted bytes from the decrypting stream
                    // and place them in a string.
                    plaintext = srDecrypt.ReadToEnd();
                }
            }
        }

        return plaintext;
    }
}
