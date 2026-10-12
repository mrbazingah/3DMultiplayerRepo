using Unity.Netcode;
using UnityEngine;

public class Prop : NetworkBehaviour
{
    [field:SerializeField] public PropType propType { get; private set; }

    public enum PropType
    {
        None,
        Lantern,
        Campfire,
        SodaCan,
        Books,
        Homeplant_01,
        Homeplant_02,
        Plushie,
        Console_01,
        Tv_01,
        Chair_01,
        Chest,
        Lamp_01,
        TrashCan_01,
        Toiletbrush,
        Toothbrush_01,
        Toothbrush_02,
        Beauty_01,
        Beauty_02,
        Beauty_04,
        Beauty_05,
        Beauty_06,
        Beauty_07,
        Beauty_08,
        Beauty_09,
        Beauty_10,
        HomeSpeaker_01,
        Soup,
        Towel_01,
    }
}
