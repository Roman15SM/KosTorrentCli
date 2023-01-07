# KosTorrentCli

KosTorrentCli - is planned to become a custom torrent console client which is currently under development for educational purposes.
For the first version, my goal is to be able to download a single torrent without seeding. Since how torrent works in detail, knowledge is slowly evaporating from the web, I'm going to create an ultimate guideline document of how it is working once I will finish this project. 
The link to that guideline will be posted here.

All documentation data based on which I'm developing this client is located here:
https://wiki.theory.org/BitTorrentSpecification

With all questions please write me at Roman15SM@gmail.com

## Roadmap
:white_check_mark: Implement Bencode parser

:white_check_mark: Implement peer id generator

:white_check_mark: Implement communication with tracker to obtain peer list

:white_check_mark: Implement and validate peer handshake

:white_check_mark: Implement piece download via "have" requests(from peer which already have all pieces)

:white_check_mark: Implement piece download from peer which sends bitfield request(peer has some pieces, but not all)

:white_check_mark: Implement file creation for single- and multi-file torrent.

:white_check_mark: Implement piece SHA-1 validation

:arrow_forward: Implement download when piece size > 16kB (some peers has a standard block size as a 32kB instead of 16kb, therefore it is a little bit hard to figure out if you receive whole block before send "request" request to finish piece download) 

:white_square_button: Implement immediate piece allocation on hard drive(for now it is in memory and allocated once all available pieces are downloaded).

:white_square_button: Speed up process by serving each peer in a separate thread(Implement concurrent piece downloading).

:white_square_button: Make it asynchronious.

:white_square_button: Make a fancy download logging like in npm for example.

:white_square_button: Implement a possibility to pause/unpause and stop downloading

:white_square_button: Implement a possibility to choose which files user wants to download.

:white_square_button: Implement seeding.

:white_square_button: Port it to linux.

:white_square_button: Write a documentation and full beginner guide.

Thanks,
Roman
