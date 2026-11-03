type CredentialDescriptorJson = { id: string; type: PublicKeyCredentialType; transports?: AuthenticatorTransport[] };
type CreationOptionsJson = Omit<PublicKeyCredentialCreationOptions, 'challenge' | 'user' | 'excludeCredentials'> & {
  challenge: string;
  user: Omit<PublicKeyCredentialUserEntity, 'id'> & { id: string };
  excludeCredentials?: CredentialDescriptorJson[];
};
type RequestOptionsJson = Omit<PublicKeyCredentialRequestOptions, 'challenge' | 'allowCredentials'> & {
  challenge: string;
  allowCredentials?: CredentialDescriptorJson[];
};

function decodeBase64Url(value: string): ArrayBuffer {
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/') + '='.repeat((4 - value.length % 4) % 4);
  return Uint8Array.from(atob(base64), character => character.charCodeAt(0)).buffer as ArrayBuffer;
}

function encodeBase64Url(value: ArrayBuffer | null | undefined): string | undefined {
  if (!value) return undefined;
  const bytes = new Uint8Array(value);
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
}

export function parseCreationOptions(json: CreationOptionsJson): PublicKeyCredentialCreationOptions {
  return {
    ...json,
    challenge: decodeBase64Url(json.challenge),
    user: { ...json.user, id: decodeBase64Url(json.user.id) },
    excludeCredentials: json.excludeCredentials?.map(item => ({ ...item, id: decodeBase64Url(item.id) })),
  };
}

export function parseRequestOptions(json: RequestOptionsJson): PublicKeyCredentialRequestOptions {
  return {
    ...json,
    challenge: decodeBase64Url(json.challenge),
    allowCredentials: json.allowCredentials?.map(item => ({ ...item, id: decodeBase64Url(item.id) })),
  };
}

export function credentialToJson(credential: PublicKeyCredential): string {
  const response = credential.response as AuthenticatorAttestationResponse | AuthenticatorAssertionResponse;
  const responseJson: Record<string, unknown> = {
    clientDataJSON: encodeBase64Url(response.clientDataJSON),
    transports: typeof (response as AuthenticatorAttestationResponse).getTransports === 'function'
      ? (response as AuthenticatorAttestationResponse).getTransports() : undefined,
  };
  const serialized: Record<string, unknown> = {
    authenticatorAttachment: credential.authenticatorAttachment,
    clientExtensionResults: credential.getClientExtensionResults(),
    id: credential.id,
    rawId: encodeBase64Url(credential.rawId),
    type: credential.type,
    response: responseJson,
  };
  if ('attestationObject' in response) {
    const attestation = response as AuthenticatorAttestationResponse & {
      getAuthenticatorData?: () => ArrayBuffer;
      getPublicKey?: () => ArrayBuffer | null;
      getPublicKeyAlgorithm?: () => number;
    };
    Object.assign(responseJson, {
      attestationObject: encodeBase64Url(attestation.attestationObject),
      authenticatorData: encodeBase64Url(attestation.getAuthenticatorData?.()),
      publicKey: encodeBase64Url(attestation.getPublicKey?.()),
      publicKeyAlgorithm: attestation.getPublicKeyAlgorithm?.(),
    });
  } else {
    const assertion = response as AuthenticatorAssertionResponse;
    Object.assign(responseJson, {
      authenticatorData: encodeBase64Url(assertion.authenticatorData),
      signature: encodeBase64Url(assertion.signature),
      userHandle: encodeBase64Url(assertion.userHandle),
    });
  }
  return JSON.stringify(serialized);
}
