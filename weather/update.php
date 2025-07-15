<?php
// Fetch weather from WeatherAPI and save to current.json
header('Content-Type: application/json');

$weatherAPIKey = "YOUR_API_KEY_HERE";
$location = "Fond du Lac, WI";
$url = "http://api.weatherapi.com/v1/current.json?key={$weatherAPIKey}&q=" . urlencode($location);

$response = @file_get_contents($url);
$data = json_decode($response, true);

if ($data && isset($data['current'])) {
    $weather = [
        'temperature' => round($data['current']['temp_f']) . '°F',
        'condition' => $data['current']['condition']['text'],
        'location' => $location,
        'timestamp' => date('Y-m-d H:i:s'),
        'last_updated' => time()
    ];
} else {
    // Fallback data if API fails
    $weather = [
        'temperature' => '73°F',
        'condition' => 'Data Unavailable',
        'location' => $location,
        'timestamp' => date('Y-m-d H:i:s'),
        'last_updated' => time()
    ];
}

// Save to current.json for VRChat to fetch
file_put_contents('current.json', json_encode($weather, JSON_PRETTY_PRINT));

// Also return the data
echo json_encode($weather);
?>
