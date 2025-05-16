using System;

public static class Encryption
{
    public static string Key = "abc";
	static Encryption(string key)
    {
        Key = key;
    }
    public string Encrypt(string text)
    {
        string result = "";
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char k = Key[i % Key.Length];
            result += (char)(c ^ k);
        }
        return result;
    }
    public string Decrypt(string text)
    {
        string result = "";
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char k = Key[i % Key.Length];
            result += (char)(c ^ k);
        }
        return result;
    }
}
