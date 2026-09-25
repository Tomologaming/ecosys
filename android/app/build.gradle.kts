plugins { id("com.android.application"); id("org.jetbrains.kotlin.android") }

android { namespace = "com.tomologaming.ecosys"; compileSdk = 35
    defaultConfig { applicationId = "com.tomologaming.ecosys"; minSdk = 26; targetSdk = 35; versionCode = 1; versionName = "0.1.0" }
    buildTypes { release { isMinifyEnabled = false } }
    kotlinOptions { jvmTarget = "17" }
}

dependencies {
    implementation("androidx.activity:activity-ktx:1.10.1")
    implementation("androidx.core:core-ktx:1.15.0")
}
