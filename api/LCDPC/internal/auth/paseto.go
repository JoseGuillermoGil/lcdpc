package auth

import (
	"encoding/hex"
	"fmt"
	"log/slog"
	"os"
	"time"

	"github.com/google/uuid"
	"github.com/o1egl/paseto"
	"golang.org/x/crypto/chacha20poly1305"
)

type TokenConfig struct {
	Issuer            string
	Audience          string
	AccessTokenTTLMin int
}

type TokenPair struct {
	AccessToken  string
	RefreshToken string
	ExpiresIn    int
}

type TokenClaims struct {
	Iss       string `json:"iss"`
	Aud       string `json:"aud"`
	Sub       string `json:"sub"`
	ProfileID string `json:"profile_id"`
	ClientID  string `json:"client_id"`
	Scope     string `json:"scope"`
	Email     string `json:"email"`
	Iat       int64  `json:"iat"`
	Exp       int64  `json:"exp"`
	Jti       string `json:"jti"`
}

type KeyService struct {
	symmetricKey []byte
}

func NewKeyService(keyPath string) (*KeyService, error) {
	ks := &KeyService{}

	if keyPath != "" {
		if err := ks.loadFromFile(keyPath); err != nil {
			return nil, fmt.Errorf("load PASETO key: %w", err)
		}
	} else {
		if err := ks.generateEphemeral(); err != nil {
			return nil, fmt.Errorf("generate PASETO key: %w", err)
		}
	}

	return ks, nil
}

func (ks *KeyService) Key() []byte {
	return ks.symmetricKey
}

func (ks *KeyService) loadFromFile(path string) error {
	data, err := os.ReadFile(path)
	if err != nil {
		return err
	}

	keyHex := string(data)
	key, err := hex.DecodeString(keyHex)
	if err != nil {
		return fmt.Errorf("invalid hex key in %s: %w", path, err)
	}
	if len(key) != chacha20poly1305.KeySize {
		return fmt.Errorf("key must be %d bytes, got %d", chacha20poly1305.KeySize, len(key))
	}

	ks.symmetricKey = key
	slog.Info("PASETO key loaded from file", "path", path)
	return nil
}

func (ks *KeyService) generateEphemeral() error {
	key := make([]byte, chacha20poly1305.KeySize)
	if _, err := randomRead(key); err != nil {
		return err
	}
	ks.symmetricKey = key
	slog.Info("ephemeral PASETO key generated (256-bit)")
	return nil
}

func GenerateAccessToken(
	key []byte,
	cfg TokenConfig,
	userID uuid.UUID,
	profileID uuid.UUID,
	clientID string,
	scope string,
	email string,
) (string, error) {
	now := time.Now().UTC()
	expires := now.Add(time.Duration(cfg.AccessTokenTTLMin) * time.Minute)

	claims := TokenClaims{
		Iss:       cfg.Issuer,
		Aud:       cfg.Audience,
		Sub:       userID.String(),
		ProfileID: profileID.String(),
		ClientID:  clientID,
		Scope:     scope,
		Email:     email,
		Iat:       now.Unix(),
		Exp:       expires.Unix(),
		Jti:       uuid.New().String(),
	}

	token, err := paseto.NewV2().Encrypt(key, claims, nil)
	if err != nil {
		return "", fmt.Errorf("encrypt paseto: %w", err)
	}

	return token, nil
}

func ValidatePasetoToken(tokenString string, key []byte, issuer, audience string) (*TokenClaims, error) {
	var claims TokenClaims
	err := paseto.NewV2().Decrypt(tokenString, key, &claims, nil)
	if err != nil {
		return nil, fmt.Errorf("decrypt paseto: %w", err)
	}

	if claims.Iss != issuer {
		return nil, fmt.Errorf("invalid issuer: expected %s, got %s", issuer, claims.Iss)
	}

	if claims.Aud != audience {
		return nil, fmt.Errorf("invalid audience: expected %s, got %s", audience, claims.Aud)
	}

	if time.Now().UTC().Unix() > claims.Exp {
		return nil, fmt.Errorf("token expired")
	}

	return &claims, nil
}

func randomRead(b []byte) (int, error) {
	f, err := os.Open("/dev/urandom")
	if err != nil {
		return 0, err
	}
	defer f.Close()
	return f.Read(b)
}
