using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CarSpawner : MonoBehaviour
{
    public List<GameObject> Cars;

    [SerializeField] private int currentcarIndex;
    public Transform carSpawnLocation;
    public Vector3 LocalSpawnScale;
    public Vector3 carDefaultRotation;

    public TextMeshProUGUI carName;

    public static Action<GameObject> onCarNumber;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        LoadCar();
        SetCar();
        onCarNumber?.Invoke(Cars[currentcarIndex].gameObject);

     
    }

    public void LoadCar()
    {
        if (PlayerPrefs.HasKey("CurrentCar"))
        {
            currentcarIndex = PlayerPrefs.GetInt("CurrentCar");
            carName.text = PlayerPrefs.GetString("CarName");

        }
        else
        {
            currentcarIndex = 0;
            PlayerPrefs.SetInt("CurrentCar", currentcarIndex);

            carName.text=  PlayerPrefs.GetString("CarName");
        }
    }
    public void SetCar()
    {

        //Model 1
        foreach (var car in Cars)
        {
            car.gameObject.SetActive(false);
        }
        Cars[currentcarIndex].gameObject.SetActive(true);

        //Model 2
        //var car= Instantiate(Cars[currentcarIndex].gameObject, carSpawnLocation.transform.position, Quaternion.identity);

        //car.transform.localScale = LocalSpawnScale;
        //car.transform.localEulerAngles = carDefaultRotation;
    }

    public void QuitGame()
    {
        Application.Quit();
    }
    public void LoadMainMenu()
    {
        SceneManager.LoadScene(0);
    }
}
