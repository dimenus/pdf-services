curl -s --request POST \
    --url https://kiriworks.us.auth0.com/oauth/token \
    --header 'content-type: application/json' \
    --data "{\"client_id\":\"$1\",\"client_secret\":\"$2\",\"audience\":\"pdf-services.apis.kiriworks.com\",\"grant_type\":\"client_credentials\"}" | jq -r .access_token
