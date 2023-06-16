# Thunderstore GitHub Deployment

To make your GitHub releases automatically upload to the Thunderstore, you can set up a `deploy.yml` inside `.github/workflows/deploy` and fill it with the following contents (adjust file names to your needs). It uses the [GreenTF/upload-thunderstore-package](https://github.com/GreenTF/upload-thunderstore-package) image.

How this works is when you make a release, it will take the first zip file of the release and send it to Thunderstore. You need to set up a repository [secret](https://docs.github.com/en/actions/security-guides/encrypted-secrets) called `TS_KEY` which has your Thunderstore team key in it.

Here's my yaml for Cheb's Necromancy:

```yml
name: Publish package

on:
  release:
    types: [published] # run when a new release is published

env:
  name: ChebsNecromancy
  jsonf: manifest.json

jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Get clean version
        run: |
          echo cleanVersion=$(echo ${{github.ref_name}} | sed s/v//g) >> $GITHUB_ENV
      - name: Check that version matches
        run: |
          if [[ "$(jq -r '.version_number' $(find ./ -name ${{env.jsonf}}))" != "${{ env.cleanVersion }}" ]]; then
            echo "::debug::${{env.cleanVersion}}"
            echo "::debug::$(cat $(find ./ -name ${{env.jsonf}}))"
            echo "::error::Version in ${{env.jsonf}} does not match tag version"
            exit 1
          fi
  publish:
    runs-on: ubuntu-latest
    needs: verify
    steps:
      - uses: actions/checkout@v3
      - run: wget https://github.com/jpw1991/chebs-necromancy/releases/download/${{ github.ref_name }}/ChebsNecromancy.${{ github.ref_name }}.zip
      - name: Upload Thunderstore Package
        uses: GreenTF/upload-thunderstore-package@v4
        with:
          community: valheim
          namespace: ChebGonaz
          name: ${{ env.name }}
          version: ${{ github.ref_name }} # This is the tag that was created in the release
          description: Cheb's Necromancy adds Necromancy to Valheim via craftable wands and structures. Minions will follow you, guard your base, and perform menial tasks.
          token: ${{ secrets.TS_KEY }} 
          deps: "ValheimModding-Jotunn@2.11.2" # dependencies separated by spaces
          categories: "Mods" # categories separated by spaces
          file: ChebsNecromancy.${{ github.ref_name }}.zip
```