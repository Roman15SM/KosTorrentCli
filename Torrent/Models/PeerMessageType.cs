public enum PeerMessageType
{
    Default = -1,
    Choke,
    UnChoke,
    Interested,
    NotInterested,
    Have,
    BitField,
    Request,
    Piece,
    Cancel,
    Port
}