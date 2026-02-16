namespace Business.Abstract
{
    /// <summary>
    /// Mesaj metinlerini AES-256-CBC ile şifreler/çözer.
    /// DB'de ciphertext saklanır, client'a plaintext döner.
    /// </summary>
    public interface IMessageEncryptionService
    {
        /// <summary>
        /// Plaintext mesajı şifreler. Her çağrıda unique IV kullanır.
        /// </summary>
        string Encrypt(string plaintext);

        /// <summary>
        /// Şifreli mesajı çözer.
        /// </summary>
        string? Decrypt(string? ciphertext);
    }
}
