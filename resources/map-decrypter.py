import hashlib
from Cryptodome.Cipher import AES
from Cryptodome.Protocol.KDF import scrypt
from Cryptodome.Util.Padding import unpad
from Cryptodome.Random import get_random_bytes

def open_encrypted_file(file_path, key_file_path):
    try:
        # Read the file (salt, nonce, tag, ciphertext)
        with open(file_path, "rb") as f:
            salt = f.read(12)  # First 12 bytes as salt
            nonce = f.read(12)  # Next 12 bytes as nonce
            tag = f.read(16)  # Next 16 bytes as tag
            ct = f.read()  # Remaining bytes as ciphertext
        
        # Derive the key using the password and salt with PBKDF2
        with open(key_file_path, "rb") as key_file:
            key = key_file.read()  # Read the encryption key from file

        # Use PBKDF2 with SHA-512 for 600,000 iterations to derive the AES key
        aes_key = hashlib.pbkdf2_hmac('sha512', key, salt, 600000, dklen=32)
        
        # Decrypt the ciphertext using AES GCM
        cipher = AES.new(aes_key, AES.MODE_GCM, nonce=nonce)
        plaintext = cipher.decrypt_and_verify(ct, tag)
        
        # Return the plaintext as bytes in a MemoryStream-like fashion
        return plaintext

    except Exception as e:
        print(f"Error: {e}")
        return None

# Example usage
file_path = "Maps.bin"  # Path to your encrypted file
key_file_path = "FILE_CRYPT_KEY.bin"  # Path to the key file

decrypted_data = open_encrypted_file(file_path, key_file_path)

if decrypted_data:
    print("Decryption successful!")
    # Save the decrypted data to a file or further process it
    with open("Maps_decrypted.bin", "wb") as output_file:
        output_file.write(decrypted_data)
else:
    print("Failed to decrypt the file.")