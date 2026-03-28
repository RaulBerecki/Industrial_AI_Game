using UnityEngine;

[CreateAssetMenu(fileName = "NewMachine", menuName = "Industrial/Machine")]
public class MachineData : ScriptableObject
{
    public string machineName;
    public int cost; // Costul pe care AI Oracle îl va folosi pentru C_optim [cite: 13]
    public GameObject prefab;
    public Sprite icon; // Pentru UI-ul de mai târziu
}