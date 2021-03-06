﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public enum DataContentFormat
{
    Int8,
    Uint8,
    Int16,
    Uint16,
    Int32,
    Uint32
}

public class Importer : MonoBehaviour
{
    public int dimX; // TODO: set good default value
    public int dimY; // TODO: set good default value
    public int dimZ; // TODO: set good default value
    public int bytesToSkip = 0;
    public DataContentFormat contentFormat;
    public string filePath;
    public int skipBytes;


    public GameObject volumeObjectPrefab;

    
    public VolumeDataset dataset;
    public VolumeRenderedObject volumeRenderedObject;

    void Start()
    {
        Import();
    }

    public void Import()
    {
        dataset = CreateDataset();
        volumeRenderedObject = CreateVolumeRenderedObject(dataset);
    }


    public VolumeDataset CreateDataset()
    {
        VolumeDataset dataset = new VolumeDataset();

        dataset.dimX = dimX;
        dataset.dimY = dimY;
        dataset.dimZ = dimZ;

        FileStream fs = new FileStream(filePath, FileMode.Open);
        BinaryReader reader = new BinaryReader(fs);

        if (skipBytes > 0)
            reader.ReadBytes(skipBytes);

        int uDimension = dimX * dimY * dimZ;
        dataset.texture = new Texture3D(dimX, dimY, dimZ, TextureFormat.RGBAFloat, false);
        dataset.texture.wrapMode = TextureWrapMode.Clamp;
        dataset.data = new int[uDimension];

        int minVal = int.MaxValue;
        int maxVal = int.MinValue;
        int val = 0;
        for (int i = 0; i < uDimension; i++)
        {
            switch (contentFormat)
            {
                case DataContentFormat.Int8:
                    val = (int)reader.ReadByte();
                    break;
                case DataContentFormat.Int16:
                    val = (int)reader.ReadInt16();
                    break;
                case DataContentFormat.Int32:
                    val = (int)reader.ReadInt32();
                    break;
                case DataContentFormat.Uint8:
                    val = (int)reader.ReadByte();
                    break;
                case DataContentFormat.Uint16:
                    val = (int)reader.ReadUInt16();
                    break;
                case DataContentFormat.Uint32:
                    val = (int)reader.ReadUInt32();
                    break;
            }
            minVal = Mathf.Min(minVal, val);
            maxVal = Mathf.Max(maxVal, val);
            dataset.data[i] = val;
        }
        Debug.Log("Loaded dataset in range: " + minVal + "  -  " + maxVal);
        Debug.Log(minVal + "  -  " + maxVal);

        dataset.minDataValue = minVal;
        dataset.maxDataValue = maxVal;

        return dataset;
    }

    public VolumeRenderedObject CreateVolumeRenderedObject(VolumeDataset dataset)
    {
        GameObject obj = GameObject.Instantiate(volumeObjectPrefab) as GameObject;
        VolumeRenderedObject volObj = obj.GetComponent<VolumeRenderedObject>();
        MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();

        volObj.dataset = dataset;

        int dimX = dataset.dimX;
        int dimY = dataset.dimY;
        int dimZ = dataset.dimZ;

        int maxRange = dataset.maxDataValue - dataset.minDataValue;

        Color[] cols = new Color[dataset.data.Length];
        for (int x = 0; x < dataset.dimX; x++)
        {
            for (int y = 0; y < dataset.dimY; y++)
            {
                for (int z = 0; z < dataset.dimZ; z++)
                {
                    int iData = x + y * dimX + z * (dimX * dimY);

                    int x1 = dataset.data[Mathf.Min(x + 1, dimX - 1) + y * dataset.dimX + z * (dataset.dimX * dataset.dimY)];
                    int x2 = dataset.data[Mathf.Max(x - 1, 0) + y * dataset.dimX + z * (dataset.dimX * dataset.dimY)];
                    int y1 = dataset.data[x + Mathf.Min(y + 1, dimY - 1) * dataset.dimX + z * (dataset.dimX * dataset.dimY)];
                    int y2 = dataset.data[x + Mathf.Max(y - 1, 0) * dataset.dimX + z * (dataset.dimX * dataset.dimY)];
                    int z1 = dataset.data[x + y * dataset.dimX + Mathf.Min(z + 1, dimZ - 1) * (dataset.dimX * dataset.dimY)];
                    int z2 = dataset.data[x + y * dataset.dimX + Mathf.Max(z - 1, 0) * (dataset.dimX * dataset.dimY)];

                    Vector3 grad = new Vector3((x2 - x1) / (float)maxRange, (y2 - y1) / (float)maxRange, (z2 - z1) / (float)maxRange);

                    cols[iData] = new Color(grad.x, grad.y, grad.z, (float)dataset.data[iData] / (float)dataset.maxDataValue);
                }
            }
        }

        dataset.texture.SetPixels(cols);
        dataset.texture.Apply();

        Texture3D tex = dataset.texture;

        const int noiseDimX = 512;
        const int noiseDimY = 512;
        Texture2D noiseTexture = NoiseTextureGenerator.GenerateNoiseTexture(noiseDimX, noiseDimY);

        TransferFunction tf = new TransferFunction();
        tf.AddControlPoint(new TFColourControlPoint(0.0f, new Color(0.11f, 0.14f, 0.13f, 1.0f)));
        tf.AddControlPoint(new TFColourControlPoint(0.2415f, new Color(0.469f, 0.354f, 0.223f, 1.0f)));
        tf.AddControlPoint(new TFColourControlPoint(0.3253f, new Color(1.0f, 1.0f, 1.0f, 1.0f)));

        tf.AddControlPoint(new TFAlphaControlPoint(0.0f, 0.0f));
        tf.AddControlPoint(new TFAlphaControlPoint(0.1787f, 0.0f));
        tf.AddControlPoint(new TFAlphaControlPoint(0.2f, 0.024f));
        tf.AddControlPoint(new TFAlphaControlPoint(0.28f, 0.03f));
        tf.AddControlPoint(new TFAlphaControlPoint(0.4f, 0.546f));
        tf.AddControlPoint(new TFAlphaControlPoint(0.547f, 0.5266f));

        tf.GenerateTexture();
        Texture2D tfTexture = tf.GetTexture();
        volObj.transferFunction = tf;

        tf.histogramTexture = HistogramTextureGenerator.GenerateHistogramTexture(dataset);

        TransferFunction2D tf2D = new TransferFunction2D();
        tf2D.AddBox(0.05f, 0.1f, 0.8f, 0.7f, Color.white, 0.4f);
        volObj.transferFunction2D = tf2D;

        meshRenderer.sharedMaterial.SetTexture("_DataTex", tex);
        meshRenderer.sharedMaterial.SetTexture("_NoiseTex", noiseTexture);
        meshRenderer.sharedMaterial.SetTexture("_TFTex", tfTexture);

        meshRenderer.sharedMaterial.EnableKeyword("MODE_DVR");
        meshRenderer.sharedMaterial.DisableKeyword("MODE_MIP");
        meshRenderer.sharedMaterial.DisableKeyword("MODE_SURF");

        return volObj;
    }
}
