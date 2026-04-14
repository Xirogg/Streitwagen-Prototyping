using UnityEngine;

public class ParticleScript : MonoBehaviour
{
    [SerializeField] private GameObject Chariot;
    [SerializeField] private float speed; 

    [SerializeField] private ParticleSystem DirtParticles;

    private bool shouldPlay;
    private float threshhold = 1f; 
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        shouldPlay = speed < threshhold;

        ApplyParticles();


    }

    private void ApplyParticles()
    {
        ChariotPhysics chariotScript = Chariot.GetComponent<ChariotPhysics>();

        speed = chariotScript.currentSpeed;
        bool wasPlaying = false;

        if (shouldPlay)
        {
            DirtParticles.Play();
            DirtParticles.loop = true; 
            print("Should play"); 

        }

        else if (!shouldPlay)
        {
            print("Should play"); 
            DirtParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            DirtParticles.loop = false; 
        }

        wasPlaying = shouldPlay; 
        
    }
}
