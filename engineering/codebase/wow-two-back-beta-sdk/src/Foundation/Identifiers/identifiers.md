# Identifiers

`IIdGenerator` produces random text identifiers; `CryptographicIdGenerator` uses unbiased cryptographic selection.
Register `services.AddSingleton<IIdGenerator, CryptographicIdGenerator>()`; choose the product's length at the call site.
The default alphabet is `IdAlphabetConstants.Alphanumeric`; custom alphabets are distinct URL-safe ASCII characters.
Length is 1..4096. Invalid length/alphabet is a configuration error. Storage must enforce uniqueness and bound collision retries.
