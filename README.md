# MStore-Server

Server handling MStore connections

# Configuration files needed:
- config.ini ( example: [config_example.ini](config_example.ini) )
- games.conf ( example: [games_example.conf](games_example.conf) )

# Files encryption:
Some important files are requiered to be encrypted using AES algirithm built into server. Files requiered to be encrypted:
- games.conf
- users.conf
- vouchers.dat

If You want to edit these files You have to decrypt them, edit and encrypt again. You can use special tool: https://github.com/MinikPLayer/AESCryptor

If You want to encrypt files when running server add "encrypt" ( without quotes ) as the first line of the file ( nothing else than this should be in the first line ). Server will automatically encrypt these files
