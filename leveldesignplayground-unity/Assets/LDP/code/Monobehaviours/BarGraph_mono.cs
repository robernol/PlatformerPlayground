using UnityEngine;

namespace DevDev.LDP.UI
{
    public class BarGraph_mono : MonoBehaviour
    {
        public BarGraph data;
        public bool reset;
        private void OnValidate()
        {
            if (reset)
            {
                reset = false;
                float highestValue = 1f / 480;
                float[] samples = new float[data.nSamples];
                for (int i = 0; i < data.nSamples; i++)
                {
                    samples[i] = (1f / (Random.Range(60, 130)));
                    if (samples[i] > highestValue)
                        highestValue = samples[i];
                }
                data.image.material.SetInt("GraphValues_Length", data.nSamples);
                data.image.material.SetFloat("_HighValue", highestValue);
                data.image.material.SetFloatArray("GraphValues", samples);
            }
        }
    }
}
