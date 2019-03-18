using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Manages the loot that bots drop.
public class LootManager : MonoBehaviour {

    public GameObject pistolAmmo, aKAmmo, parts, healthPack;

    private float[] dropProbabilites = { 0.5f, 0.25f, 0.2f, 0.05f };

    private ShootBehaviour player1Gun, player2Gun;
    private PlayerStateManager player1StateManager, player2StateManager;
    private InventoryManager player1Inventory, player2Inventory;
    private GameObject player1, player2;

    // Use this for initialization
    void Start () { // Just reference setup.
        player1 = GameObject.FindGameObjectWithTag("Player1");
        player1Gun = player1.GetComponentInChildren<ShootBehaviour>();
        player1StateManager = player1.GetComponent<PlayerStateManager>();
        player1Inventory = player1.GetComponent<InventoryManager>();

        player2 = GameObject.FindGameObjectWithTag("Player2");
        if(player2 != null) {
            player2Gun = player2.GetComponentInChildren<ShootBehaviour>();
            player2StateManager = player2.GetComponent<PlayerStateManager>();
            player2Inventory = player2.GetComponent<InventoryManager>();
        }
    }
	
	// Update is called once per frame
	void Update () {

    }

    public void SpawnLoot(Transform transform) {
        int pistolAmmoAmount = 0, aKAmmoAmount = 0, partsAmount = 0;

        switch ((int)findProbability(dropProbabilites)) {
            //Different probability brackets based on drop probabilities
            case 0: //50%
                pistolAmmoAmount = 5;
                aKAmmoAmount = 0;
                partsAmount = 5;
                break;
            case 1: //25%
                pistolAmmoAmount = 10;
                aKAmmoAmount = 0;
                partsAmount = 10;
                break;
            case 2: //20%
                pistolAmmoAmount = 10;
                aKAmmoAmount = 10;
                partsAmount = 20;
                break;
            case 3: //5%
                pistolAmmoAmount = 20;
                aKAmmoAmount = 20;
                partsAmount = 40;
                break;
        }

        var pistrolAmmoLoot = Instantiate(pistolAmmo,
            new Vector3(transform.position.x + 1f, transform.position.y, transform.position.z),
            Quaternion.identity);
        pistrolAmmoLoot.GetComponent<Loot>().itemAmount = pistolAmmoAmount;

        var partsLoot = Instantiate(parts,
            new Vector3(transform.position.x - 1f, transform.position.y, transform.position.z),
            Quaternion.identity);
        partsLoot.GetComponent<Loot>().itemAmount = partsAmount;

        if (aKAmmoAmount > 0) {
            var aKAmmoloot = Instantiate(aKAmmo,
            new Vector3(transform.position.x, transform.position.y, transform.position.z + 1f),
            Quaternion.identity);
            aKAmmoloot.GetComponent<Loot>().itemAmount = aKAmmoAmount;
        }

    }

    public void ApplyLoot(InventoryItems item, int amount, string playerTag) {
        // Call inventory system here and add to it.
        switch (playerTag) {
            case "Player1":
                player1Inventory.AddItemToInventory(item, amount);
                break;
            case "Player2":
                player2Inventory.AddItemToInventory(item, amount);
                break;
            default:
                throw new System.Exception("PlayerTag String wrong: " + playerTag);
        }
    }

    public int CanPlayerLootItem(InventoryItems item, int amount, string playerTag) {
        switch(playerTag) {
            case "Player1":
                return player1Inventory.CanPickUpItemAndHowMuch(item, amount);
            case "Player2":
                return player2Inventory.CanPickUpItemAndHowMuch(item, amount);
            default:
                throw new System.Exception("PlayerTag String wrong: " + playerTag);
        }
    }


    private float findProbability(float[] probs) {
        float total = 0;

        foreach (float elem in probs) {
            total += elem;
        }

        float randomPoint = Random.value * total;

        for (int i = 0; i < probs.Length; i++) {
            if (randomPoint < probs[i]) {
                return i;
            }
            else {
                randomPoint -= probs[i];
            }
        }

        return probs.Length - 1;
    }
}
