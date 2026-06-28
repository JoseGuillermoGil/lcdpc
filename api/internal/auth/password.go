package auth

import (
	"crypto/sha256"
	"encoding/hex"
	"fmt"

	"golang.org/x/crypto/pbkdf2"
	"crypto/rand"
)

func HashPassword(password string) (string, error) {
	salt := make([]byte, 16)
	if _, err := rand.Read(salt); err != nil {
		return "", fmt.Errorf("generate salt: %w", err)
	}
	hash := pbkdf2.Key([]byte(password), salt, 100_000, 32, sha256.New)
	return fmt.Sprintf("PBKDF2$100000$SHA256$%s$%s",
		encodeB64(salt), encodeB64(hash)), nil
}

func VerifyPassword(password, storedHash string) bool {
	// Format: PBKDF2$iterations$algorithm$salt$hash
	parts := splitN(storedHash, '$', 5)
	if len(parts) != 5 || parts[0] != "PBKDF2" {
		return false
	}

	var iterations int
	fmt.Sscanf(parts[1], "%d", &iterations)
	salt := decodeB64(parts[3])
	expectedHash := decodeB64(parts[4])

	actualHash := pbkdf2.Key([]byte(password), salt, iterations, len(expectedHash), sha256.New)
	return subtleCompare(actualHash, expectedHash)
}

func HashToken(token string) string {
	h := sha256.Sum256([]byte(token))
	return hex.EncodeToString(h[:])
}

func GenerateOpaqueToken() (string, error) {
	b := make([]byte, 48)
	if _, err := rand.Read(b); err != nil {
		return "", fmt.Errorf("generate token: %w", err)
	}
	return encodeB64URL(b), nil
}

func GenerateOtp() string {
	var n uint32
	b := make([]byte, 4)
	rand.Read(b)
	n = uint32(b[0])<<24 | uint32(b[1])<<16 | uint32(b[2])<<8 | uint32(b[3])
	return fmt.Sprintf("%06d", n%1000000)
}

func HashOtp(otp string) string {
	h := sha256.Sum256([]byte(otp))
	return hex.EncodeToString(h[:])
}

// Helpers

func encodeB64(b []byte) string {
	return stdBase64(b)
}

func encodeB64URL(b []byte) string {
	return rawURLBase64(b)
}

func decodeB64(s string) []byte {
	b, _ := stdBase64Decode(s)
	return b
}

func splitN(s string, sep byte, n int) []string {
	result := make([]string, 0, n)
	start := 0
	for i := 0; i < len(s) && len(result) < n-1; i++ {
		if s[i] == sep {
			result = append(result, s[start:i])
			start = i + 1
		}
	}
	result = append(result, s[start:])
	return result
}

func subtleCompare(a, b []byte) bool {
	if len(a) != len(b) {
		return false
	}
	var v byte
	for i := 0; i < len(a); i++ {
		v |= a[i] ^ b[i]
	}
	return v == 0
}

func stdBase64(b []byte) string {
	const table = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
	var result []byte
	for i := 0; i < len(b); i += 3 {
		switch len(b) - i {
		case 3:
			result = append(result,
				table[b[i]>>2],
				table[((b[i]&0x03)<<4)|(b[i+1]>>4)],
				table[((b[i+1]&0x0F)<<2)|(b[i+2]>>6)],
				table[b[i+2]&0x3F])
		case 2:
			result = append(result,
				table[b[i]>>2],
				table[((b[i]&0x03)<<4)|(b[i+1]>>4)],
				table[(b[i+1]&0x0F)<<2])
		case 1:
			result = append(result,
				table[b[i]>>2],
				table[(b[i]&0x03)<<4])
		}
	}
	return string(result)
}

func rawURLBase64(b []byte) string {
	const table = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"
	var result []byte
	for i := 0; i < len(b); i += 3 {
		switch len(b) - i {
		case 3:
			result = append(result,
				table[b[i]>>2],
				table[((b[i]&0x03)<<4)|(b[i+1]>>4)],
				table[((b[i+1]&0x0F)<<2)|(b[i+2]>>6)],
				table[b[i+2]&0x3F])
		case 2:
			result = append(result,
				table[b[i]>>2],
				table[((b[i]&0x03)<<4)|(b[i+1]>>4)],
				table[(b[i+1]&0x0F)<<2])
		case 1:
			result = append(result,
				table[b[i]>>2],
				table[(b[i]&0x03)<<4])
		}
	}
	return string(result)
}

func stdBase64Decode(s string) ([]byte, error) {
	const table = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
	// Build reverse lookup
	inv := make([]int8, 256)
	for i := range inv {
		inv[i] = -1
	}
	for i, c := range table {
		inv[c] = int8(i)
	}

	// Remove padding
	s = trimRight(s, '=')

	result := make([]byte, 0, len(s)*3/4)
	buf := 0
	bits := 0
	for _, c := range s {
		val := inv[c]
		if val < 0 {
			continue
		}
		buf = (buf << 6) | int(val)
		bits += 6
		if bits >= 8 {
			bits -= 8
			result = append(result, byte(buf>>bits))
			buf &= (1 << bits) - 1
		}
	}
	return result, nil
}

func trimRight(s string, c byte) string {
	for len(s) > 0 && s[len(s)-1] == c {
		s = s[:len(s)-1]
	}
	return s
}
